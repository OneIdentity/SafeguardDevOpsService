using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    internal class AddonManager : IHostedService, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;

        private IAddonService _devOpsAddon;

        public AddonManager(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
        }

        public void Dispose()
        {

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (LoadAddonService())
            {
                Task.Run(async () => await _devOpsAddon.RunAddonServiceAsync(cancellationToken), cancellationToken);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private bool LoadAddonService()
        {
            try
            {
                if (!File.Exists(WellKnownData.AddonServicePath))
                    return false;

                var assembly = Assembly.LoadFrom(WellKnownData.AddonServicePath);

                var watchdogClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass 
                                                                   && t.Name.Equals(WellKnownData.AddonServiceClassName) 
                                                                   && typeof(IAddonService).IsAssignableFrom(t));

                if (watchdogClass != null)
                {
                    _logger.Information($"Loading Add-on service from path {WellKnownData.AddonServicePath}.");
                    var watchdogService = (IAddonService) Activator.CreateInstance(watchdogClass);

                    if (watchdogService == null)
                    {
                        _logger.Warning($"Unable to instantiate the Add-on service from {WellKnownData.AddonServicePath}");
                    }
                    else
                    {
                        _devOpsAddon = watchdogService;
                        _devOpsAddon.SetLogger(_logger);
                        _devOpsAddon.SetDatabase(_configDb);

                        _logger.Information($"Successfully loaded the Add-on Service {_devOpsAddon.DisplayName} : {_devOpsAddon.Description}.");

                        try
                        {
                            var pluginVersion = ReadPluginVersion(WellKnownData.AddonServicePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning($"Failed to load Watchdog service {Path.GetFileName(WellKnownData.AddonServicePath)}: {ex.Message}.");
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load Watchdog service {Path.GetFileName(WellKnownData.AddonServicePath)}: {ex.Message}.");
            }

            return false;
        }

        private string ReadPluginVersion(string pluginPath)
        {
            var version = "Unknown";
            var manifestPath = Path.Combine(Path.GetDirectoryName(pluginPath) ?? pluginPath, WellKnownData.ManifestPattern);
            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifest = File.ReadAllText(manifestPath);
                    var pluginManifest = JsonHelper.DeserializeObject<PluginManifest>(manifest);
                    if (pluginManifest != null)
                    {
                        version = pluginManifest.Version ?? version;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to read the manifest file for {pluginPath}. {ex.Message}");
                }
            }

            return version;
        }


    }
}
