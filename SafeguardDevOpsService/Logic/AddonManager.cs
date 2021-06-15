using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;

namespace OneIdentity.DevOps.Logic
{
    internal class AddonManager : IHostedService, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private readonly ISafeguardLogic _safeguardLogic;

        private IAddonService _devOpsAddon;

        public AddonManager(IConfigurationRepository configDb, ISafeguardLogic safeguardLogic)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
            _safeguardLogic = safeguardLogic;
        }

        public void Dispose()
        {

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var files = Directory.GetFiles(WellKnownData.ProgramDataPath, WellKnownData.AddonDeleteFile, SearchOption.AllDirectories);
            if (files.Any())
            {
                foreach (var addonPath in files)
                {
                    Directory.Delete(Path.GetDirectoryName(addonPath), true);
                }
            }
            else
            {
                var addons = _configDb.GetAllAddons();

                foreach (var addon in addons)
                {
                    if (LoadAddonService(addon))
                    {
                        Task.Run(async () => await _devOpsAddon.RunAddonServiceAsync(cancellationToken),
                            cancellationToken);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private bool LoadAddonService(Addon addon)
        {
            var addonAssemblyPath = Path.Combine(WellKnownData.ProgramDataPath, addon.Manifest.DestinationFolder, addon.Manifest.Assembly);

            try
            {
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
                        _devOpsAddon = addonService;
                        _devOpsAddon.SetLogger(_logger);

                        _devOpsAddon.Name = addon.Manifest.Name;
                        _devOpsAddon.DisplayName = addon.Manifest.DisplayName;
                        _devOpsAddon.Description = addon.Manifest.Description;
                        _devOpsAddon.AddOn = addon;

                        //Subscribe for property changes in the addon object
                        _devOpsAddon.AddOn.PropertyChanged += AddonPropertyChangedHandler;

                        _logger.Information($"Successfully loaded the Add-on Service {_devOpsAddon.DisplayName} : {_devOpsAddon.Description}.");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load the add-on service {addon.Manifest.Name}: {ex.Message}.");
            }

            return false;
        }

        public void AddonPropertyChangedHandler(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _logger.Information($"Addon accounts have changed.  Saving changes.");

            _configDb.SaveAddon(sender as Addon);

            _devOpsAddon.AddOn.PropertyChanged -= AddonPropertyChangedHandler;
            _devOpsAddon.AddOn.IsDirty = false;
            _devOpsAddon.AddOn.PropertyChanged += AddonPropertyChangedHandler;

        }

    }
}
