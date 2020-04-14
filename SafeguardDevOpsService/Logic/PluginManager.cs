using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Logic
{
    internal class PluginManager : IDisposable, IPluginManager
    {
        private const string PluginDirName = "ExternalPlugins";
        private static readonly Dictionary<string,ILoadablePlugin> LoadedPlugins = new Dictionary<string, ILoadablePlugin>();
        private readonly Serilog.ILogger _logger;
        private FileSystemWatcher _watcher;

        public string ServiceName => GetType().Name;

        private readonly IConfigurationRepository _configDb;

        internal PluginManager(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if (Path.GetExtension(e.FullPath).ToLower().Equals(WellKnownData.DllExtension))
                LoadRegisterPlugin(e.FullPath);
        }

        public void Run()
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            var dirPath = Path.GetDirectoryName(exePath);
            var pluginDirPath = Path.Combine(dirPath, PluginDirName);

            _watcher = new FileSystemWatcher()
            {
                Path = pluginDirPath,
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "*.*",
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnChanged;

            DetectPlugins(pluginDirPath);
        }

        public void SetConfigurationForPlugin(string name)
        {
            if (LoadedPlugins.ContainsKey(name))
            {
                var pluginInstance = LoadedPlugins[name];
                var pluginInfo = _configDb.GetPluginByName(name);
                var configuration = pluginInfo?.Configuration;
                if (configuration != null)
                {
                    pluginInstance.SetPluginConfiguration(configuration);
                }
                _logger.Information($"Plugin {name} configured successfully.");
            }
            else
            {
                _logger.Error($"Plugin configuration failed. No plugin {name} found.");
            }
        }

        public bool SendPassword(string name, string accountName, SecureString password)
        {
            if (LoadedPlugins.ContainsKey(name))
            {
                var pluginInstance = LoadedPlugins[name];
                if (pluginInstance != null)
                    return pluginInstance.SetPassword(accountName, password.ToInsecureString());
            }
            else
            {
                _logger.Error($"Send password to plugin failed.  No plugin {name} found.");
            }

            return false;
        }

        private void DetectPlugins(string pluginDirPath)
        {
            if (Directory.Exists(pluginDirPath))
            {
                var pluginFiles = Directory.GetFiles(pluginDirPath, WellKnownData.DllPattern);
                foreach (var file in pluginFiles)
                {
                    LoadRegisterPlugin(file);
                }
            }
        }

        private void LoadRegisterPlugin(string pluginPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(pluginPath);
                foreach (var type in assembly.GetTypes()
                    .Where(t => t.IsClass &&
                                t.Name.Equals(WellKnownData.PluginInfoClassName) &&
                                typeof(ILoadablePlugin).IsAssignableFrom(t)))
                {
                    _logger.Information($"Loading plugin from path {pluginPath}.");
                    var plugin = (ILoadablePlugin) Activator.CreateInstance(type);

                    var name = plugin.Name;
                    var description = plugin.Description;
                    plugin.SetLogger(_logger);

                    _logger.Information($"Successfully loaded plugin {name} : {description}.");

                    var pluginInstance = plugin;

                    var pluginInfo = _configDb.GetPluginByName(name);

                    if (!LoadedPlugins.ContainsKey(name))
                    {
                        LoadedPlugins.Add(name, pluginInstance);
                    }
                    else
                    {
                        pluginInstance = LoadedPlugins[name];
                    }

                    if (pluginInfo == null)
                    {
                        pluginInfo = new Plugin
                        {
                            Name = name,
                            Description = description,
                            Configuration = pluginInstance.GetPluginInitialConfiguration()
                        };

                        _configDb.SavePluginConfiguration(pluginInfo);

                        _logger.Information($"Discovered new unconfigured plugin {Path.GetFileName(pluginPath)}.");

                    }
                    else
                    {
                        var configuration = pluginInfo.Configuration;
                        if (configuration != null)
                        {
                            pluginInstance.SetPluginConfiguration(configuration);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load plugin {Path.GetFileName(pluginPath)}: {ex.Message}.");
            }
        }

        public void Dispose()
        {
            _watcher.Changed -= OnChanged;
            _watcher.Dispose();
        }
    }
}
