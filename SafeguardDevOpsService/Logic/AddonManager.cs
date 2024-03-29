﻿using System;
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
        private readonly Func<IAddonLogic> _addonLogic;

        public AddonManager(IConfigurationRepository configDb, Func<IAddonLogic> addonLogic)
        {
            _configDb = configDb;
            _addonLogic = addonLogic;
            _logger = Serilog.Log.Logger;
        }

        public void Dispose()
        {

        }

        public void Run()
        {
            CleanUpDeletedAddons();

            try {
                if (Directory.Exists(WellKnownData.AddonServiceStageDirPath) && !File.Exists(WellKnownData.DeleteAddonStagingDir))
                {
                    // If the addon successfully upgraded, then restart immediately.
                    if (_addonLogic().UpgradeAddon())
                    {
                        Task.Run(() => Environment.Exit(27)).Wait();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to upgrade the staged Add-on: {ex.Message}.");
            }

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

        public void ShutdownAddon(Addon addon)
        {
            if (addon != null && LoadedAddons.ContainsKey(addon.Name))
            {
                var addonInstance = LoadedAddons[addon.Name];
                addonInstance?.Unload();
            }
        }

        public void StartAddon(Addon addon)
        {
            if (addon != null && LoadedAddons.ContainsKey(addon.Name))
            {
                var addonInstance = LoadedAddons[addon.Name];
                Task.Run(async () => await addonInstance.RunAddonServiceAsync(addonInstance.AddOn.ServiceCancellationToken.Token), addonInstance.AddOn.ServiceCancellationToken.Token);
            }
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
                        addonService.Logger = _logger;

                        addonService.Name = addon.Manifest.Name;
                        addonService.DisplayName = addon.Manifest.DisplayName;
                        addonService.Description = addon.Manifest.Description;
                        addonService.AddOn = addon;

                        // Make sure that the addon services are provided with the latest TLS certificates if needed.
                        addonService.TlsCertificates = _configDb.GetWebSslPemCertificate();

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
            try
            {
                var directories = Directory.GetDirectories(WellKnownData.ProgramDataPath);
                foreach (var dir in directories)
                {
                    try 
                    {
                        var files = Directory.GetFiles(dir, WellKnownData.AddonDeleteFile, SearchOption.TopDirectoryOnly);
                        if (files.Any())
                        {
                            foreach (var addonPath in files)
                            {
                                Directory.Delete(Path.GetDirectoryName(addonPath), true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Failed to query for deleted add-ons in directory {dir}: {ex.Message}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to query for deleted add-ons: {ex.Message}.");
            }

            try
            {
                if (File.Exists(WellKnownData.DeleteAddonStagingDir))
                {
                    Directory.Delete(WellKnownData.AddonServiceStageDirPath, true);
                    _logger.Information($"Cleaning up Add-on staging folder {WellKnownData.AddonServiceStageDirPath}.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to clean up the Add-on staging folder: {ex.Message}.");
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
