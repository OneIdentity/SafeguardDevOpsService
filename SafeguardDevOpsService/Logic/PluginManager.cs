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
        private static readonly Dictionary<string,ILoadablePlugin> LoadedPlugins = new Dictionary<string, ILoadablePlugin>(StringComparer.InvariantCultureIgnoreCase);

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

        private void OnDirectoryCreate(object source, FileSystemEventArgs e)
        {
            if (Directory.Exists(e.FullPath))
            {
                //Give the file copy a few seconds to settle down.  If it takes longer than this to copy, 
                // the plugin may not load.  But since the copy is a local file system copy, hopefully 
                // this is enough time.  Otherwise restart will catch it.
                Thread.Sleep(2000);
                DetectPlugin(e.FullPath);
            }
        }

        public void Run()
        {
            CleanUpDeletedPlugins();

            var pluginDirPath = WellKnownData.PluginDirPath;
            Directory.CreateDirectory(pluginDirPath);

            if (Directory.Exists(WellKnownData.PluginStageDirPath))
            {
                var directories = Directory.GetDirectories(WellKnownData.PluginStageDirPath);
                if (directories.Length > 0)
                {
                    foreach (var directory in directories)
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(directory);
                            var dirPath = Path.Combine(pluginDirPath, dirInfo.Name);
                            if (Directory.Exists(dirPath))
                                Directory.Delete(dirPath, true);

                            Directory.Move(directory, Path.Combine(pluginDirPath, dirInfo.Name));
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Logger.Information($"Failed to install plugin {directory}. {ex.Message}");
                        }
                    }
                }
                Directory.Delete(WellKnownData.PluginStageDirPath, true);
                Serilog.Log.Logger.Information($"Installed staged plugins to {pluginDirPath}.");
            }

            Serilog.Log.Logger.Information($"Watching {pluginDirPath} for plugins that should be loaded.");

            _watcher = new FileSystemWatcher()
            {
                Path = pluginDirPath,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                Filter = "*.*",
                EnableRaisingEvents = true
            };
            _watcher.Created += OnDirectoryCreate;

            DetectPlugins(pluginDirPath);
        }

        public void CleanUpDeletedPlugins()
        {
            // If the DeleteAllPlugins file exists, just remove the entire external plugins directory.
            if (File.Exists(WellKnownData.DeleteAllPlugins))
            {
                try
                {
                    Directory.Delete(WellKnownData.PluginDirPath, true);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to clean up the external plugins directory. {ex.Message}");
                }
                return;
            }

            var plugins = _configDb.GetAllPlugins();
            foreach (var plugin in plugins)
            {
                if (plugin.IsDeleted)
                {
                    try
                    {
                        _configDb.DeletePluginByName(plugin.Name, true);

                        var dirPath = Path.Combine(WellKnownData.PluginDirPath, plugin.Name);
                        if (Directory.Exists(dirPath)) 
                            Directory.Delete(dirPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to clean up the external plugin {plugin.Name}. {ex.Message}");
                    }
                }

            }
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
                var pluginDirectories = Directory.GetDirectories(pluginDirPath);
                foreach (var directory in pluginDirectories)
                {
                    DetectPlugin(directory);
                }
            }
        }

        private void DetectPlugin(string pluginDirPath)
        {
            var pluginFiles = Directory.GetFiles(pluginDirPath, WellKnownData.DllPattern);
            foreach (var file in pluginFiles)
            {
                LoadRegisterPlugin(file);
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

        public bool TestPluginVaultConnection(ISafeguardConnection sgConnection, string pluginName)
        {
            var plugin = _configDb.GetPluginByName(pluginName);

            if (_safeguardLogic.IsLoggedIn() && plugin?.VaultAccountId != null)
            {
                var pluginInstance = LoadedPlugins[pluginName];
                if (pluginInstance != null)
                {
                    try
                    {
                        RefreshPluginCredential(sgConnection, plugin);
                        if (pluginInstance.TestVaultConnection())
                        {
                            _logger.Error($"Test connection for plugin {pluginName} successful.");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to test the connection for plugin {pluginName}.  {ex.Message}");
                    }
                }
                else
                {
                    _logger.Error($"Failed to test the connection for plugin {pluginName}.  Plugin information is missing.");
                }
            }
            else
            {
                _logger.Error($"Failed to test the connection for plugin {pluginName}.  Missing login or vault account information.");
            }

            return false;
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
                    _logger.Error(ex, $"Failed to read the manifest file for {pluginPath}. {ex.Message}");
                }
            }

            return version;
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
                    var displayName = plugin.DisplayName;
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
                        var pluginVersion = ReadPluginVersion(pluginPath);

                        if (pluginInfo == null)
                        {
                            pluginInfo = new Plugin
                            {
                                Name = name,
                                DisplayName = displayName,
                                Description = description,
                                Configuration = pluginInstance.GetPluginInitialConfiguration(),
                                Version = pluginVersion
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

                            if (!string.Equals(pluginInfo.Name, name, StringComparison.OrdinalIgnoreCase) 
                                || !string.Equals(pluginInfo.Description, description, StringComparison.OrdinalIgnoreCase) 
                                || !string.Equals(pluginInfo.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)
                                || !string.Equals(pluginInfo.Version, pluginVersion, StringComparison.OrdinalIgnoreCase))
                            {
                                pluginInfo.Name = name;
                                pluginInfo.DisplayName = displayName;
                                pluginInfo.Description = description;
                                pluginInfo.Version = pluginVersion;

                                _configDb.SavePluginConfiguration(pluginInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, $"Failed to configure plugin {Path.GetFileName(pluginPath)}: {ex.Message}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to load plugin {Path.GetFileName(pluginPath)}: {ex.Message}.");
            }
        }

        public void RefreshPluginCredentials(ISafeguardConnection sgConnection)
        {
            var plugins = _configDb.GetAllPlugins();
        
            foreach (var plugin in plugins)
            {
                RefreshPluginCredential(sgConnection, plugin);
            }
        }
        
        private void RefreshPluginCredential(ISafeguardConnection sgConnection, Plugin plugin)
        {
            if (_safeguardLogic.IsLoggedIn() && plugin.VaultAccountId.HasValue)
            {
                try
                {
                    var a2aAccount = _safeguardLogic.GetA2ARetrievableAccount(sgConnection, plugin.VaultAccountId.Value,
                        A2ARegistrationType.Vault);
                    if (a2aAccount != null)
                    {
                        SendPluginVaultCredentials(plugin.Name, a2aAccount.ApiKey);
                        return;
                    }

                    _logger.Error(
                        $"Failed to refresh the credential for plugin {plugin.Name} account {plugin.VaultAccountId}.");
                }
                catch (Exception ex)
                {
                    var msg =
                        $"Failed to refresh the api key for {plugin.Name} account {plugin.VaultAccountId}: {ex.Message}";
                    _logger.Error(ex, msg);
                }
            }
        }

        public bool IsLoadedPlugin(string name)
        {
            return (LoadedPlugins.ContainsKey(name));
        }

        public bool IsDisabledPlugin(string name)
        {
            if (LoadedPlugins.ContainsKey(name))
            {
                var plugin = _configDb.GetPluginByName(name);
                return plugin.IsDisabled;
            }

            return true;
        }

        public void Dispose()
        {
            _watcher.Created -= OnDirectoryCreate;
            _watcher.Dispose();
        }
    }
}
