using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Exceptions;

namespace OneIdentity.DevOps.Logic
{
    internal class AddonLogic : IAddonLogic
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private IDeployAddon _deployAddon;
        private IUndeployAddon _undeployAddon;


        public AddonLogic(IConfigurationRepository configDb)
        {
            _logger = Serilog.Log.Logger;
            _configDb = configDb;
        }

        private DevOpsException LogAndException(string msg, Exception ex = null)
        {
            _logger.Error(msg);
            return new DevOpsException(msg, ex);
        }

        public void InstallAddon(string base64Addon, bool force)
        {
            if (base64Addon == null)
                throw LogAndException("Addon cannot be null");

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
                throw LogAndException("Failed to find the add-on in the database.");
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
                _logger.Warning("Unable to create addon delete file.");
            }

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

        private void InstallAddon(ZipArchive zipArchive, bool force)
        {
            var manifestEntry = zipArchive.GetEntry(WellKnownData.ManifestPattern);
            if (manifestEntry == null)
            {
                throw LogAndException("Failed to find the manifest for the add-on.");
            }

            using (var reader = new StreamReader(manifestEntry.Open()))
            {
                var manifest = reader.ReadToEnd();
                var addonManifest = JsonHelper.DeserializeObject<AddonManifest>(manifest);
                if (addonManifest != null)
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
                    zipArchive.ExtractToDirectory(WellKnownData.AddonServiceStageDirPath, true);

                    addon = new Addon()
                    {
                        Name = addonManifest.Name,
                        Manifest = addonManifest,
                    };
                    _configDb.SaveAddon(addon);

                    DeployAddon(addonManifest, WellKnownData.AddonServiceStageDirPath);
                }
                else
                {
                    throw LogAndException(
                        $"Add-on package does not contain a {WellKnownData.ManifestPattern} file.");
                }
            }
        }

        private void DeployAddon(AddonManifest addonManifest, string tempFolder)
        {
            try
            {
                var addonPath = Path.Combine(WellKnownData.AddonServiceStageDirPath, addonManifest.SourceFolder, addonManifest.Assembly);
                if (!File.Exists(addonPath))
                {
                    throw LogAndException("Failed to find the add-on module.");
                }

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
                        _deployAddon = deployAddon;
                        _deployAddon.SetLogger(_logger);
                        _deployAddon.SetTempDirectory(tempFolder);

                        _deployAddon.Deploy(addonManifest, _configDb.GetWebSslPemCertificate());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to deploy the Add-on service {addonManifest.Name}: {ex.Message}.");
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
                        _configDb.DeleteAddonByName(addon.Manifest.Name);
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
                        _undeployAddon = undeployAddon;
                        _undeployAddon.SetLogger(_logger);

                        _undeployAddon.Undeploy(addon.Manifest);
                        _configDb.DeleteAddonByName(addon.Manifest.Name);
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
