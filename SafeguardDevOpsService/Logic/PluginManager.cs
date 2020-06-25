using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Logic
{
    internal class PluginManager : IDisposable, IPluginManager
    {
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
            {
                //Give the file copy just a half of a second to settle down.
                Thread.Sleep(500);
                LoadRegisterPlugin(e.FullPath);
            }
        }

        public void Run()
        {
            var pluginDirPath = WellKnownData.PluginDirPath;
            Directory.CreateDirectory(pluginDirPath);

            if (Directory.Exists(WellKnownData.PluginStageDirPath))
            {
                var files = Directory.GetFiles(WellKnownData.PluginStageDirPath);
                if (files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        File.Move(file, Path.Combine(pluginDirPath, Path.GetFileName(file)), true);
                    }
                }
                Directory.Delete(WellKnownData.PluginStageDirPath, true);
                Serilog.Log.Logger.Information($"Installed staged plugins to {pluginDirPath}.");
            }

            Serilog.Log.Logger.Information($"Watching {pluginDirPath} for plugins that should be loaded.");

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

                    try
                    {
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
                    catch (Exception ex)
                    {
                        _logger.Warning($"Failed to configure plugin {Path.GetFileName(pluginPath)}: {ex.Message}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load plugin {Path.GetFileName(pluginPath)}: {ex.Message}.");
            }
        }

        public bool IsLoadedPlugin(string name)
        {
            return (LoadedPlugins.ContainsKey(name));
        }

        public void Dispose()
        {
            _watcher.Changed -= OnChanged;
            _watcher.Dispose();
        }
    }
}
