using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    internal class WatchdogManager : IHostedService, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;

        private IDevOpsWatchdog _devOpsWatchdog;

        public WatchdogManager(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
        }

        public void Dispose()
        {

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            LoadWatchdogService();

            Task.Run(async () => await _devOpsWatchdog.RunWatchdogAsync(cancellationToken), cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void LoadWatchdogService()
        {
            try
            {
                if (!File.Exists(WellKnownData.WatchdogServicePath))
                    return;

                var assembly = Assembly.LoadFrom(WellKnownData.WatchdogServicePath);

                var watchdogClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass 
                                                                   && t.Name.Equals(WellKnownData.WatchdogServiceClassName) 
                                                                   && typeof(IDevOpsWatchdog).IsAssignableFrom(t));

                if (watchdogClass != null)
                {
                    _logger.Information($"Loading Watchdog service from path {WellKnownData.WatchdogServicePath}.");
                    var watchdogService = (IDevOpsWatchdog) Activator.CreateInstance(watchdogClass);

                    if (watchdogService == null)
                    {
                        _logger.Warning($"Unable to instantiate Watchdog service from {WellKnownData.WatchdogServicePath}");
                    }
                    else
                    {
                        _devOpsWatchdog = watchdogService;
                        _devOpsWatchdog.SetLogger(_logger);
                        _devOpsWatchdog.SetDatabase(_configDb);

                        _logger.Information($"Successfully loaded Watchdog Service {_devOpsWatchdog.DisplayName} : {_devOpsWatchdog.Description}.");

                        try
                        {
                            var pluginVersion = ReadPluginVersion(WellKnownData.WatchdogServicePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning($"Failed to load Watchdog service {Path.GetFileName(WellKnownData.WatchdogServicePath)}: {ex.Message}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load Watchdog service {Path.GetFileName(WellKnownData.WatchdogServicePath)}: {ex.Message}.");
            }
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
