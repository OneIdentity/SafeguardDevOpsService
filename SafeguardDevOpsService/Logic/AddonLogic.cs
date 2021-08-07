using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Exceptions;

namespace OneIdentity.DevOps.Logic
{
    internal class AddonLogic : IAddonLogic
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private readonly IAddonManager _addonManager;
        private readonly ISafeguardLogic _safeguardLogic;

        public AddonLogic(IConfigurationRepository configDb, IAddonManager addonManager, ISafeguardLogic safeguardLogic)
        {
            _logger = Serilog.Log.Logger;
            _configDb = configDb;
            _addonManager = addonManager;
            _safeguardLogic = safeguardLogic;
        }

        private DevOpsException LogAndException(string msg, Exception ex = null)
        {
            _logger.Error(msg);
            return new DevOpsException(msg, ex);
        }

        public void InstallAddon(string base64Addon, bool force)
        {
            if (!_safeguardLogic.ValidateLicense())
            {
                throw LogAndException("Invalid licenses.");
            }

            if (base64Addon == null)
                throw LogAndException("Add-on cannot be null");

            var bytes = Convert.FromBase64String(base64Addon);

            try
            {
                using (var inputStream = new MemoryStream(bytes))
                using (var zipArchive = new ZipArchive(inputStream, ZipArchiveMode.Read))
                {
                    InstallAddon(zipArchive, force);
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to install the vault plugin. {ex.Message}");
            }
        }

        public void InstallAddon(IFormFile formFile, bool force)
        {
            if (!_safeguardLogic.ValidateLicense())
            {
                throw LogAndException("Invalid licenses.");
            }

            if (formFile.Length <= 0)
                throw LogAndException("Add-on cannot be null or empty");

            try
            {
                using (var inputStream = formFile.OpenReadStream())
                using (var zipArchive = new ZipArchive(inputStream, ZipArchiveMode.Read))
                {
                    InstallAddon(zipArchive, force);
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to install the Add-on plugin. {ex.Message}");
            }
        }

        public void RemoveAddon(string name)
        {
            var addon = _configDb.GetAddonByName(name);
            if (addon == null)
            {
                throw LogAndException("Failed to find the Add-on in the database.");
            }

            _logger.Information("Saving the remove token to disk. The Add-on will be delete after a reboot.");
            try
            {
                using (File.Create(Path.Combine(WellKnownData.ProgramDataPath, addon.Manifest.DestinationFolder,
                    WellKnownData.AddonDeleteFile)))
                {}
            }
            catch
            {
                _logger.Warning("Unable to create Add-on delete file.");
            }

            // Stop the addon service before undeploying the addon.
            _addonManager.ShutdownAddon(addon);

            // Deleting the addon renders the asset accounts useless since the vault is being
            //  deleted as well.
            _safeguardLogic.DeleteAssetAccounts(null, _configDb.AssetId ?? 0);

            UndeployAddon(addon);
        }

        public IEnumerable<Addon> GetAddons()
        {
            var addonsInternal = _configDb.GetAllAddons();
            var addons = addonsInternal.Select(x => JsonHelper.DeserializeObject<Addon>(JsonHelper.SerializeObject(x)));
            return addons;
        }

        public Addon GetAddon(string addonName)
        {
            var addonInternal = _configDb.GetAddonByName(addonName);
            var addon = JsonHelper.DeserializeObject<Addon>(JsonHelper.SerializeObject(addonInternal));
            return addon;
        }

        public AddonStatus GetAddonStatus(string addonName)
        {
            var isLicensed = _safeguardLogic.ValidateLicense();
            var addon = _configDb.GetAddonByName(addonName);
            if (addon != null)
            {
                var addonStatus = _addonManager.GetAddonStatus(addon, isLicensed);

                if (!SafeguardHasVaultCredentials(addon))
                {
                    // If the addon reported that the addon is ready but
                    //  the credentials have not been pushed yet, clear
                    //  the healthy status.
                    if (addonStatus.IsReady)
                        addonStatus.HealthStatus.Clear();
                    addonStatus.IsReady = false;
                    addonStatus.HealthStatus.Add("[Configuration] The add-on needs to be configured.");
                }

                return addonStatus;
            }

            return new AddonStatus()
            {
                IsReady = false,
                HealthStatus = new List<string> {"[Configuration] Unknown."}
            };
        }

        private bool SafeguardHasVaultCredentials(Addon addon)
        {
            var accountsPushedToSpp = false;

            var assetId = _configDb.AssetId;
            if (assetId != null && assetId > 0)
            {
                var addonAccounts = addon.VaultCredentials;
                var accounts = _safeguardLogic.GetAssetAccounts(null, assetId.Value).ToArray();
                if (accounts.Any())
                {
                    accountsPushedToSpp = accounts.All(x => addonAccounts.ContainsKey(x.Name));
                }
            }

            return accountsPushedToSpp;
        }

        public bool ConfigureDevOpsAddOn(string addonName)
        {
            var assetId = _configDb.AssetId;
            if ((assetId ?? 0) == 0)
                return false;

            var sg = _safeguardLogic.Connect();

            try
            {
                var addon = _configDb.GetAddonByName(addonName);
                if (addon.VaultCredentials.Any() && !SafeguardHasVaultCredentials(addon))
                {
                    var tasks = new List<Task>();
                    foreach (var vaultCredential in addon.VaultCredentials)
                    {
                        var newAccount = new AssetAccount()
                        {
                            Name = vaultCredential.Key,
                            AssetId = assetId.Value
                        };
                        var p = addon.VaultCredentials[newAccount.Name];

                        tasks.Add(Task.Run(() =>
                        {
                            newAccount = _safeguardLogic.AddAssetAccount(sg, newAccount);
                            _safeguardLogic.SetAssetAccountPassword(sg, newAccount, p);
                        }));
                    }

                    if (tasks.Any())
                        Task.WaitAll(tasks.ToArray());
                }
            }
            finally
            {
                sg.Dispose();
            }

            return true;
        }

        private void InstallAddon(ZipArchive zipArchive, bool force)
        {
            var manifestEntry = zipArchive.GetEntry(WellKnownData.ManifestPattern);
            if (manifestEntry == null)
            {
                throw LogAndException("Failed to find the manifest for the Add-on.");
            }

            using (var reader = new StreamReader(manifestEntry.Open()))
            {
                var manifest = reader.ReadToEnd();
                var addonManifest = JsonHelper.DeserializeObject<AddonManifest>(manifest);
                if (addonManifest != null && ValidateManifest(addonManifest))
                {
                    var addon = _configDb.GetAddonByName(addonManifest.Name);
                    if (addon != null)
                    {
                        if (force)
                        {
                            _configDb.DeleteAddonByName(addonManifest.Name);
                        }
                        else
                        {
                            _logger.Warning($"Add-on {addon.Name} already exists. ");
                            return;
                        }
                    }

                    RestartManager.Instance.ShouldRestart = true;
                    _logger.Debug($"Extracting Add-on to {WellKnownData.AddonServiceStageDirPath}");
                    if (!Directory.Exists(WellKnownData.AddonServiceStageDirPath))
                        Directory.CreateDirectory(WellKnownData.AddonServiceStageDirPath);

                    zipArchive.ExtractToDirectory(WellKnownData.AddonServiceStageDirPath, true);

                    // TODO: Remove this renaming of slashes if ZipFile creation in the pipeline can be made to
                    //       use the proper slashes and ZipArchive can properly decode them
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        var addonSubPath = Path.Combine(WellKnownData.AddonServiceStageDirPath, "Addon");
                        if (!Directory.Exists(addonSubPath))
                            Directory.CreateDirectory(addonSubPath);
                        foreach (var entry in Directory.EnumerateFileSystemEntries(WellKnownData.AddonServiceStageDirPath))
                        {
                            var entryFilename = Path.GetFileName(entry);
                            if (entryFilename.StartsWith(@"Addon\"))
                            {
                                File.Move(Path.Combine(WellKnownData.AddonServiceStageDirPath, entryFilename),
                                    Path.Combine(addonSubPath, entryFilename.Substring(6)));
                            }
                        }
                    }

                    _logger.Debug($"Add-on manifest name: {addonManifest.Name}");
                    addon = new Addon()
                    {
                        Name = addonManifest.Name,
                        Manifest = addonManifest
                    };
                    _configDb.SaveAddon(addon);

                    DeployAddon(addonManifest, WellKnownData.AddonServiceStageDirPath);
                }
                else
                {
                    throw LogAndException(
                        $"Add-on package does not contain a valid {WellKnownData.ManifestPattern} file.");
                }
            }
        }

        private bool ValidateManifest(AddonManifest addonManifest)
        {
            return addonManifest != null
                   && addonManifest.GetType().GetProperties()
                       .Where(pi => pi.PropertyType == typeof(string))
                       .Select(pi => (string) pi.GetValue(addonManifest))
                       .All(value => !string.IsNullOrEmpty(value))
                   && addonManifest.Type.Equals(WellKnownData.AddOnUploadType, StringComparison.OrdinalIgnoreCase);
        }

        private void DeployAddon(AddonManifest addonManifest, string tempFolder)
        {
            try
            {
                _logger.Debug($"Add-on manifest source folder: {addonManifest.SourceFolder}");
                _logger.Debug($"Add-on manifest assembly: {addonManifest.Assembly}");
                var addonPath = Path.Combine(WellKnownData.AddonServiceStageDirPath, addonManifest.SourceFolder, addonManifest.Assembly);
                _logger.Debug($"Looking for Add-on module at {addonPath}");
                if (!File.Exists(addonPath))
                {
                    throw LogAndException("Failed to find the Add-on module.");
                }

                _logger.Debug($"Loading Add-on assembly from {addonPath}");
                var assembly = Assembly.LoadFrom(addonPath);

                var deployAddonClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass
                                                                   && t.Name.Equals(addonManifest.DeployClassName)
                                                                   && typeof(IDeployAddon).IsAssignableFrom(t));

                if (deployAddonClass != null)
                {
                    _logger.Information($"Loading the Add-on service from path {addonPath}.");
                    var deployAddon = (IDeployAddon) Activator.CreateInstance(deployAddonClass);

                    if (deployAddon == null)
                    {
                        _logger.Warning($"Unable to instantiate the Add-on service from {addonPath}");
                    }
                    else
                    {
                        deployAddon.SetLogger(_logger);
                        deployAddon.SetTempDirectory(tempFolder);

                        deployAddon.Deploy(addonManifest, _configDb.GetWebSslPemCertificate());
                    }
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to deploy the Add-on service {addonManifest.Name}: {ex.Message}.");
            }

        }

        private void UndeployAddon(Addon addon)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().
                    SingleOrDefault(a => a.GetName().Name == addon.Manifest.AssemblyName);

                if (assembly == null)
                {
                    _logger.Warning(
                        $"Failed to find the reference to the loaded Add-on {addon.Manifest.AssemblyName}.  Attempting to load the Add-on.");
                    try
                    {
                        assembly = Assembly.LoadFrom(Path.Combine(WellKnownData.ProgramDataPath,
                            addon.Manifest.DestinationFolder, addon.Manifest.Assembly));
                    }
                    catch
                    {
                        _logger.Error(
                            $"Failed to load the Add-on {addon.Manifest.AssemblyName}. The Add-on code may be missing. Cleaning up the Add-on from the Secrets Broker.");
                        if (addon.Manifest?.Name != null)
                            _configDb.DeleteAddonByName(addon.Manifest.Name);
                        if (addon.Manifest?.PluginName != null)
                        _configDb.DeletePluginByName(addon.Manifest.PluginName);
                        return;
                    }
                }


                var undeployAddonClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass
                                                                               && t.Name.Equals(addon.Manifest.UndeployClassName)
                                                                               && typeof(IUndeployAddon).IsAssignableFrom(t));

                if (undeployAddonClass != null)
                {
                    _logger.Information($"Loading the {addon.Manifest.UndeployClassName} class.");
                    var undeployAddon = (IUndeployAddon) Activator.CreateInstance(undeployAddonClass);

                    if (undeployAddon == null)
                    {
                        _logger.Warning($"Unable to instantiate the Add-on service {addon.Manifest.UndeployClassName} class.");
                    }
                    else
                    {
                        undeployAddon.SetLogger(_logger);

                        undeployAddon.Undeploy(addon.Manifest);
                        if (addon.Manifest?.Name != null)
                            _configDb.DeleteAddonByName(addon.Manifest.Name);
                        if (addon.Manifest?.PluginName != null)
                            _configDb.DeletePluginByName(addon.Manifest.PluginName);
                    }
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to undeploy the Add-on service {addon.Manifest.ServiceClassName}: {ex.Message}.");
            }
        }
    }
}
