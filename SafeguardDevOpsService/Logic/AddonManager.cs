using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    internal class AddonManager : IAddonManager, IDisposable
    {
        private static readonly Dictionary<string,IAddonService> LoadedAddons = new Dictionary<string, IAddonService>(StringComparer.OrdinalIgnoreCase);

        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;

        public AddonManager(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
        }

        public void Dispose()
        {

        }

        public void Run()
        {
            CleanUpDeletedAddons();

            var addons = _configDb.GetAllAddons();

            foreach (var addon in addons)
            {
                LoadAddonService(addon);
            }
        }

        public AddonStatus GetAddonStatus(Addon addon, bool isLicensed)
        {
            if (addon != null && LoadedAddons.ContainsKey(addon.Name))
            {
                var addonInstance = LoadedAddons[addon.Name];
                var status = addonInstance?.GetHealthStatus();
                if (status != null)
                {
                    return new AddonStatus()
                    {
                        IsReady = isLicensed && status.Item1,
                        HealthStatus = isLicensed ? status.Item2 : new List<string>() { "Not Licensed" }
                    };
                }
            }

            return null;
        }

        private bool LoadAddonService(Addon addon)
        {
            if (addon?.Manifest?.DestinationFolder == null || addon.Manifest.Assembly == null)
            {
                _logger.Information("Found an invalid add-on path. Failed to load the add-on.");
                return false;
            }

            try
            {
                var addonAssemblyPath = Path.Combine(WellKnownData.ProgramDataPath, addon.Manifest.DestinationFolder, addon.Manifest.Assembly);

                if (!File.Exists(addonAssemblyPath))
                    return false;

                var assembly = Assembly.LoadFrom(addonAssemblyPath);

                var addonClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass 
                                                                   && t.Name.Equals(addon.Manifest.ServiceClassName) 
                                                                   && typeof(IAddonService).IsAssignableFrom(t));

                if (addonClass != null)
                {
                    _logger.Information($"Loading Add-on service from path {addonAssemblyPath}.");
                    var addonService = (IAddonService) Activator.CreateInstance(addonClass);

                    if (addonService == null)
                    {
                        _logger.Error($"Unable to instantiate the Add-on service from {addonAssemblyPath}");
                    }
                    else
                    {
                        addonService.SetLogger(_logger);

                        addonService.Name = addon.Manifest.Name;
                        addonService.DisplayName = addon.Manifest.DisplayName;
                        addonService.Description = addon.Manifest.Description;
                        addonService.AddOn = addon;

                        //Subscribe for property changes in the addon object
                        addonService.AddOn.PropertyChanged += AddonPropertyChangedHandler;

                        if (!LoadedAddons.ContainsKey(addonService.Name))
                        {
                            LoadedAddons.Add(addonService.Name, addonService);
                        }
                        else
                        {
                            LoadedAddons[addonService.Name] = addonService;
                        }

                        Task.Run(async () => await addonService.RunAddonServiceAsync(addonService.AddOn.ServiceCancellationToken.Token), addonService.AddOn.ServiceCancellationToken.Token);

                        _logger.Information($"Successfully loaded the Add-on Service {addonService.DisplayName} : {addonService.Description}.");

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to load the Add-on service {addon.Manifest.Name}: {ex.Message}.");
            }

            return false;
        }

        private void CleanUpDeletedAddons()
        {
            var files = Directory.GetFiles(WellKnownData.ProgramDataPath, WellKnownData.AddonDeleteFile, SearchOption.AllDirectories);
            if (files.Any())
            {
                foreach (var addonPath in files)
                {
                    Directory.Delete(Path.GetDirectoryName(addonPath), true);
                }
            }
        }

        public void AddonPropertyChangedHandler(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is Addon addon)
            {
                if (addon.CredentialsUpdated)
                {
                    addon.CredentialsUpdated = false;

                    _logger.Information("Addon accounts have changed.  Saving changes.");

                    Dictionary<string, string> credentials = new Dictionary<string, string>();
                    foreach (var credential in addon.VaultCredentials)
                    {
                        credentials.Add(WellKnownData.DevOpsCredentialName(credential.Key, _configDb.SvcId),
                            credential.Value);
                    }

                    addon.VaultCredentials = credentials;
                    _configDb.SaveAddon(addon);
                }
            }
        }

    }
}
