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
        private static readonly Dictionary<string,LoadedPlugin> LoadedPlugins = new Dictionary<string, LoadedPlugin>();
        private static readonly string stageLoader = "stageloader";

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
            if (Path.GetExtension(e.FullPath).ToLower().Equals(WellKnownData.ManifestPattern))
                LoadRegisterPlugin(e.FullPath);
        }

        public void Run()
        {
            var pluginDirPath = WellKnownData.PluginDirPath;
            Directory.CreateDirectory(pluginDirPath);
            Serilog.Log.Logger.Information($"Watching {pluginDirPath} for plugins that should be loaded.");

            _watcher = new FileSystemWatcher()
            {
                Path = pluginDirPath,
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "Manifest.json",
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
                    pluginInstance.LoadablePlugin.SetPluginConfiguration(configuration);
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
                    return pluginInstance.LoadablePlugin.SetPassword(accountName, password.ToInsecureString());
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
                var pluginFiles = Directory.GetFiles(pluginDirPath, WellKnownData.ManifestPattern);
                foreach (var file in pluginFiles)
                {
                    LoadRegisterPlugin(file);
                }
            }
        }

        private PluginManifest ReadPluginManifest(string pluginPath)
        {
            try
            {
                string manifest = File.ReadAllText(pluginPath);
                var pluginManifest = JsonHelper.DeserializeObject<PluginManifest>(manifest);
                return pluginManifest;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to read the plugin manifest {Path.GetFileName(pluginPath)}: {ex.Message}.");
                return null;
            }
        }

        private void LoadRegisterPlugin(string pluginPath)
        {
            var manifest = ReadPluginManifest(pluginPath);
            if (manifest == null)
                return;

            AssemblyLoader stagingDomain = null;

            try
            {
                //Give the file copy just a half of a second to settle down.
                Thread.Sleep(500);

                var dirInfo = Directory.GetParent(pluginPath);

                var assemblyPath = Path.Join(dirInfo.FullName, manifest.Assembly);
                stagingDomain = new AssemblyLoader(dirInfo.FullName);
//                AssemblyName assemblyName = new AssemblyName() {CodeBase = Path.Join(dirInfo.FullName, manifest.Assembly)};

                var assembly = stagingDomain.LoadFromAssemblyPath(assemblyPath);
//                var assembly = stagingDomain.LoadFromAssemblyName(assemblyName);
//                var assembly = Assembly.LoadFrom(pluginPath);

//                var t = assembly.GetTypes();
//                var b = typeof(ILoadablePlugin).IsAssignableFrom(t[0]);

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

                    var pluginInstance = new LoadedPlugin() {LoadablePlugin = plugin};

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
                                Configuration = pluginInstance.LoadablePlugin.GetPluginInitialConfiguration()
                            };

                            _configDb.SavePluginConfiguration(pluginInfo);

                            _logger.Information($"Discovered new unconfigured plugin {Path.GetFileName(pluginPath)}.");

                        }
                        else
                        {
                            var configuration = pluginInfo.Configuration;
                            if (configuration != null)
                            {
                                pluginInstance.LoadablePlugin.SetPluginConfiguration(configuration);
                            }
                        }

                        pluginInstance.AssemblyLoader = stagingDomain;
                        stagingDomain = null;
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
            finally
            {
                if (stagingDomain != null)
                {
//                    AppDomain.Unload(stagingDomain);
                }
            }

        }

        public void UnloadPlugin(string name)
        {
            if (LoadedPlugins.ContainsKey(name))
            {
                var pluginInstance = LoadedPlugins[name];
                pluginInstance.LoadablePlugin.Unload();

                pluginInstance.AssemblyLoader.Unload();
                pluginInstance.AssemblyLoader = null;
                LoadedPlugins.Remove(name);

                _logger.Information($"Plugin {name} configured successfully.");
            }
            else
            {
                _logger.Error($"Plugin configuration failed. No plugin {name} found.");
            }
        }

        public void Dispose()
        {
            _watcher.Changed -= OnChanged;
            _watcher.Dispose();
        }
    }
}
