using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security;
using System.Security.Cryptography.X509Certificates;
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
        private readonly ISafeguardLogic _safeguardLogic;

        internal PluginManager(IConfigurationRepository configDb, ISafeguardLogic safeguardLogic)
        {
            _configDb = configDb;
            _safeguardLogic = safeguardLogic;
            _logger = Serilog.Log.Logger;
        }

        bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return CertificateHelper.CertificateValidation(sender, certificate, chain, sslPolicyErrors, _logger, _configDb);
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            var fileExtension = Path.GetExtension(e.FullPath);
            if (fileExtension != null && fileExtension.ToLower().Equals(WellKnownData.DllExtension))
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
            }
            else
            {
                _logger.Error($"Plugin configuration failed. No plugin {name} found.");
            }
        }

        public bool SendPassword(string name, string assetName, string accountName, SecureString password)
        {
            if (LoadedPlugins.ContainsKey(name))
            {
                var pluginInstance = LoadedPlugins[name];
                if (pluginInstance != null)
                    return pluginInstance.SetPassword(assetName, accountName, password.ToInsecureString());
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

        public void SendPluginVaultCredentials(string name, string apiKey)
        {
            var pluginInstance = LoadedPlugins[name];
            if (pluginInstance != null)
            {
                if (apiKey != null)
                {
                    var credential = GetPluginCredential(name, apiKey);
                    if (credential != null)
                    {
                        pluginInstance.SetVaultCredential(credential);
                        return;
                    }
                    _logger.Error(
                        $"Failed to provide the plugin {name} with the vault credential plugin.");
                }
                else
                {
                    _logger.Error(
                        $"Failed to get the vault credential api key for plugin {name}.");
                }
            }
        }

        private string GetPluginCredential(string name, string apiKey)
        {
            var sppAddress = _configDb.SafeguardAddress;
            var userCertificate = _configDb.UserCertificateBase64Data;
            var passPhrase = _configDb.UserCertificatePassphrase?.ToSecureString();
            var apiVersion = _configDb.ApiVersion;
            var ignoreSsl = _configDb.IgnoreSsl;

            if (sppAddress != null && userCertificate != null && apiVersion.HasValue && ignoreSsl.HasValue && apiKey != null)
            {
                // connect to Safeguard
                var a2AContext = (ignoreSsl == true) ? 
                    Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, apiVersion.Value, true) : 
                    Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, CertificateValidationCallback, apiVersion.Value);
                using (var password = a2AContext.RetrievePassword(apiKey.ToSecureString()))
                {
                    return password.ToInsecureString();
                }
            }

            _logger.Error($"Unable to get the vault credential for {name} plugin.");
            return null;
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

                    if (plugin == null)
                    {
                        _logger.Warning($"Unable to instantiate plugin from {pluginPath}");
                        continue;
                    }
                    
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

        public void RefreshPluginCredentials()
        {
            var plugins = _configDb.GetAllPlugins();
        
            foreach (var plugin in plugins)
            {
                RefreshPluginCredential(plugin);
            }
        }
        
        private void RefreshPluginCredential(Plugin plugin)
        {
            if (_safeguardLogic.IsLoggedIn() && plugin.VaultAccountId.HasValue)
            {
                try
                {
                    var a2aAccount = _safeguardLogic.GetA2ARetrievableAccount(plugin.VaultAccountId.Value,
                        A2ARegistrationType.Vault);
                    if (a2aAccount != null)
                    {
                        SendPluginVaultCredentials(plugin.Name, a2aAccount.ApiKey);
                        return;
                    }
        
                    _logger.Error($"Failed to refresh the credential for plugin {plugin.Name} account {plugin.VaultAccountId}.");
                }
                catch (Exception ex)
                {
                    var msg = $"Failed to refresh the api key for {plugin.Name} account {plugin.VaultAccountId}: {ex.Message}";
                    _logger.Error(msg);
                }
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
