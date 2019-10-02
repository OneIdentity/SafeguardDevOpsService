using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Security;
using OneIdentity.Common;
using OneIdentity.SafeguardDevOpsService.ConfigDb;
using OneIdentity.SafeguardDevOpsService.Data;
using OneIdentity.SafeguardDevOpsService.Impl;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.SafeguardDevOpsService.Plugins
{
    public class PluginManager : IDisposable, IPluginManager
    {
        private static Dictionary<string,ILoadablePlugin> _loadedPlugins = new Dictionary<string, ILoadablePlugin>();
        private FileSystemWatcher _watcher = null;
        private Serilog.ILogger _logger;

        public string ServiceName => GetType().Name;


        private readonly IConfigurationRepository _configurationRepository;

        public PluginManager(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository;
            _logger = Serilog.Log.Logger;
        }

        public void Dispose()
        {
            _watcher.Changed -= OnChanged;
            _watcher.Dispose();
        }

        public void Run()
        {
            var exePath = Assembly.GetExecutingAssembly().Location;

            _watcher = new FileSystemWatcher()
            {
                Path = Path.GetDirectoryName(exePath),
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "*.*",
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnChanged;

            DetectPlugins(exePath);

        }

        public void SetConfigurationforPlugin(string name)
        {
            if (_loadedPlugins.ContainsKey(name))
            {
                var pluginInstance = _loadedPlugins[name];
                var pluginInfo = _configurationRepository.GetPluginByName(name);
                var configuration = pluginInfo?.Configuration;
                if (configuration != null)
                {
                    pluginInstance.SetPluginConfiguration(configuration);
                }
            }
        }

        public bool SendPassword(string name, string accountName, SecureString password)
        {
            if (_loadedPlugins.ContainsKey(name))
            {
                var pluginInstance = _loadedPlugins[name];
                if (pluginInstance != null)
                    return pluginInstance.SetPassword(accountName, password.ToInsecureString());
            }
            return false;
        }

        private void DetectPlugins(string exePath)
        {
            var dirPath = Path.GetDirectoryName(exePath);
            if (Directory.Exists(dirPath))
            {
                var pluginFiles = Directory.GetFiles(dirPath, WellKnownData.dllPattern);
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

                Type[] types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.Name.Equals(WellKnownData.PluginInfoClassName) && type.IsClass)
                    {
                        var plugin = (ILoadablePlugin)Activator.CreateInstance(type);

                        var name = plugin.Name;
                        var description = plugin.Description;
                        Dictionary<string,string> configuration = null;
                        ILoadablePlugin pluginInstance = plugin;

                        if (!_loadedPlugins.ContainsKey(name))
                        {
                            _loadedPlugins.Add(name, pluginInstance);
                        } else
                        {
                            pluginInstance = _loadedPlugins[name];
                        }

                        var pluginInfo = _configurationRepository.GetPluginByName(name);
                        if (pluginInfo == null)
                        {
                            pluginInfo = new Plugin() { Name = name, Description = description };
                            _configurationRepository.SavePluginConfiguration(pluginInfo);
                            configuration = new Dictionary<string, string>();
                        } else
                        {
                            configuration = pluginInfo.Configuration;
                        }

                        if (configuration != null)
                            pluginInstance.SetPluginConfiguration(configuration);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load plugin {Path.GetFileName(pluginPath)}: {ex.Message}.");
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if (Path.GetExtension(e.FullPath).ToLower().Equals(WellKnownData.dllExtension))
                LoadRegisterPlugin(e.FullPath);
        }
    }
}
