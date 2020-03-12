using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Security;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Logic;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Plugins
{
    internal class PluginManager : IDisposable, IPluginManager
    {
        private const string PluginDirName = "ExternalPlugins";
        private static readonly Dictionary<string,ILoadablePlugin> LoadedPlugins = new Dictionary<string, ILoadablePlugin>();
        private readonly Serilog.ILogger _logger;
        private FileSystemWatcher _watcher;

        public string ServiceName => GetType().Name;

        private readonly IConfigurationRepository _configurationRepository;

        internal PluginManager(IConfigurationRepository configurationRepository)
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
            var pluginDirPath = Path.Combine(exePath, PluginDirName);

            _watcher = new FileSystemWatcher()
            {
                Path = Path.GetDirectoryName(exePath),
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
                var pluginInfo = _configurationRepository.GetPluginByName(name);
                var configuration = pluginInfo?.Configuration;
                if (configuration != null)
                {
                    pluginInstance.SetPluginConfiguration(configuration);
                }
                _logger.Information($"Plugin {name} configured successfully.");
            }
            else
            {
                _logger.Error($"Plugin configuration failed.  No plugin {name} found.");
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
            var dirPath = Path.GetDirectoryName(pluginDirPath);
            if (Directory.Exists(dirPath))
            {
                var pluginFiles = Directory.GetFiles(dirPath, WellKnownData.DllPattern);
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
                    // dbConfig = get configuration from database
                    // if(dbConfig == null)
                    //      //This plugin was never configured.
                    //      1. Retrieve initial configuration from plugin.
                    //      2. Save configuration in DB so someone can go to the application and fill outvalues for this plugin.
                    // else
                    //      //This plugin was configured - maybe do some test or check a field set to true in DB
                    //      1. Pass configuration to the plugin
                    //      2. Save plugin to _loadedPlugins.
                    //
                    //      DONE


                    if (type.Name.Equals(WellKnownData.PluginInfoClassName) && type.IsClass)
                    {
                        _logger.Information($"Loading plugin from path {pluginPath}.");
                        var plugin = (ILoadablePlugin)Activator.CreateInstance(type);

                        var name = plugin.Name;
                        var description = plugin.Description;
                        plugin.SetLogger(_logger);

                        _logger.Information($"Successfully loaded plugin {name} : {description}.");

                        ILoadablePlugin pluginInstance = plugin;

                        var pluginInfo = _configurationRepository.GetPluginByName(name);

                        if (!LoadedPlugins.ContainsKey(name))
                        {
                            LoadedPlugins.Add(name, pluginInstance);
                        }
                        else
                        {
                            //If an instance of the plugin was already found, then use the existing instance.
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

                            _configurationRepository.SavePluginConfiguration(pluginInfo);

                            _logger.Information($"Discovered new unconfigured plugin {Path.GetFileName(pluginPath)}.");
                            
                        } else
                        {
                            var configuration = pluginInfo.Configuration;
                            if (configuration != null)
                            {
                                pluginInstance.SetPluginConfiguration(configuration);                            
                            }
                        }
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
            if (Path.GetExtension(e.FullPath).ToLower().Equals(WellKnownData.DllExtension))
                LoadRegisterPlugin(e.FullPath);
        }
    }
}
