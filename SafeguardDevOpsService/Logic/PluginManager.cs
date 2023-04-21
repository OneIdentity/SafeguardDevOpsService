using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Security;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Exceptions;
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

        private DevOpsException LogAndException(string msg, Exception ex = null)
        {
            _logger.Error(msg);
            return new DevOpsException(msg, ex);
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
                    var rootName = plugin.RootPluginName;
                    var pluginInstances = _configDb.GetPluginInstancesByName(rootName);
                    if (pluginInstances == null || pluginInstances.Count() == 1)
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
                    else if (!plugin.IsRootPlugin)
                    {
                        _configDb.DeletePluginByName(plugin.Name, true);
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
                    pluginInstance.AssignedCredentialType = pluginInfo.AssignedCredentialType;
                }
            }
            else
            {
                _logger.Error($"Plugin configuration failed. No plugin {name} found.");
            }
        }

        public bool SendCredential(string name, string assetName, string accountName, string[] credential, CredentialType assignedCredentialType, string altAccountName = null)
        {
            if (LoadedPlugins.ContainsKey(name))
            {
                var pluginInstance = LoadedPlugins[name];
                if (pluginInstance != null && credential.Length > 0)
                {
                    switch (assignedCredentialType)
                    {
                        case CredentialType.Password:
                            return pluginInstance.SetPassword(assetName, accountName, credential.FirstOrDefault(), altAccountName);
                        case CredentialType.SshKey:
                            return pluginInstance.SetSshKey(assetName, accountName, credential.FirstOrDefault(), altAccountName);
                        case CredentialType.ApiKey:
                            return pluginInstance.SetApiKey(assetName, accountName, credential, altAccountName);
                    }
                }
            }
            else
            {
                _logger.Error($"Send credential to plugin failed.  No plugin {name} found or missing credential.");
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
                var pluginInfo = LoadRegisterPlugin(file);
                if (pluginInfo != null)
                {
                    LoadRegisterPluginInstances(pluginInfo);
                }
            }
        }

        private void SendPluginVaultCredentials(string name, string apiKey)
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

        private void SendPluginVaultCredentialOnly(string name, string credential)
        {
            var pluginInstance = LoadedPlugins[name];
            if (pluginInstance != null && credential != null)
            {
                pluginInstance.SetVaultCredential(credential);
            }
            else
            {
                _logger.Error(
                    $"Failed to get the plugin instance {name}.");
            }
        }

        public bool TestPluginVaultConnection(ISafeguardConnection sgConnection, string pluginName)
        {
            var plugin = _configDb.GetPluginByName(pluginName);

            if (plugin?.VaultAccountId != null)
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
            var credential = GetAccountCredential(name, apiKey, CredentialType.Password);
            return credential?.FirstOrDefault();
        }

        public string[] GetAccountCredential(string name, string a2AApiKey, CredentialType assignedType)
        {
            var sppAddress = _configDb.SafeguardAddress;
            var userCertificate = _configDb.UserCertificateBase64Data;
            var passPhrase = _configDb.UserCertificatePassphrase?.ToSecureString();
            var apiVersion = _configDb.ApiVersion;
            var ignoreSsl = _configDb.IgnoreSsl;

            if (sppAddress != null && userCertificate != null && apiVersion.HasValue && ignoreSsl.HasValue && a2AApiKey != null)
            {
                try
                {
                    // connect to Safeguard
                    var a2AContext = (ignoreSsl == true)
                        ? Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase,
                            apiVersion.Value, true)
                        : Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase,
                            CertificateValidationCallback, apiVersion.Value);
                    switch (assignedType)
                    {
                        case CredentialType.Password:
                        {
                            using var password = a2AContext.RetrievePassword(a2AApiKey.ToSecureString());
                            return new[] { password.ToInsecureString() };
                        }
                        case CredentialType.SshKey:
                        {
                            using var sshKey = a2AContext.RetrievePrivateKey(a2AApiKey.ToSecureString());
                            return new[] { sshKey.ToInsecureString().ReplaceLineEndings(string.Empty) };
                        }
                        case CredentialType.ApiKey:
                        {
                            var apiKeySecrets = a2AContext.RetrieveApiKeySecret(a2AApiKey.ToSecureString());
                            var apiKeys = new List<string>();
                            foreach (var apiKey in apiKeySecrets)
                            {
                                using (apiKey)
                                {
                                    apiKeys.Add(apiKey.ToString());
                                }
                            }

                            return apiKeys.ToArray();
                        }
                        default:
                            _logger.Error($"Failed to recognize the assigned credential for the {name} plugin.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Unable to get the credential for {name} plugin: {ex.Message}.", ex);
                }
            }

            _logger.Error($"Unable to get the credential for {name} plugin.");
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

        private Plugin LoadRegisterPlugin(string pluginPath)
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
                    var loadablePlugin = (ILoadablePlugin) Activator.CreateInstance(type);

                    if (loadablePlugin == null)
                    {
                        _logger.Warning($"Unable to instantiate plugin from {pluginPath}");
                        continue;
                    }
                    
                    var name = loadablePlugin.Name;
                    var displayName = loadablePlugin.DisplayName;
                    var description = loadablePlugin.Description;
                    var supportedCredentialTypes = loadablePlugin.SupportedCredentialTypes;
                    loadablePlugin.SetLogger(_logger);

                    var pluginInstance = loadablePlugin;

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
                                SupportedCredentialTypes = supportedCredentialTypes,
                                Configuration = pluginInstance.GetPluginInitialConfiguration(),
                                IsRootPlugin = true,
                                Version = pluginVersion
                            };

                            pluginInfo = ConfigureIfSystemOwned(pluginInfo);

                            _configDb.SavePluginConfiguration(pluginInfo);

                            _logger.Information($"Discovered new unconfigured plugin {Path.GetFileName(pluginPath)}.");

                        }
                        else
                        {
                            var configuration = pluginInfo.Configuration;
                            if (configuration != null)
                            {
                                var newConfiguration = pluginInstance.GetPluginInitialConfiguration();
                                // Check to see if the new configuration is the same as the old configuration. if not,
                                //  then copy over the values from the old configuration to the new one.
                                if (!(configuration.Count == newConfiguration.Count &&
                                      configuration.Keys.SequenceEqual(newConfiguration.Keys)))
                                {
                                    foreach (var item in configuration)
                                    {
                                        if (newConfiguration.ContainsKey(item.Key))
                                        {
                                            newConfiguration[item.Key] = item.Value;
                                        }
                                    }

                                    configuration = newConfiguration;
                                    pluginInfo.Configuration = newConfiguration;
                                    _configDb.SavePluginConfiguration(pluginInfo);
                                }

                                pluginInstance.SetPluginConfiguration(configuration);
                                pluginInstance.AssignedCredentialType = pluginInfo.AssignedCredentialType;

                            }

                            _configDb.SetRootPlugin(pluginInstance.Name, true);
                            pluginInfo = UpdatePluginInfo(new Plugin
                            {
                                Name = name, 
                                DisplayName = displayName, 
                                Description = description, 
                                SupportedCredentialTypes = supportedCredentialTypes, 
                                Version = pluginVersion
                            }, pluginInfo);
                        }

                        return pluginInfo;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex,
                            $"Failed to configure plugin {Path.GetFileName(pluginPath)}: {ex.Message}.");
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // Most likely, this is not a .NET DLL.
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to load plugin {Path.GetFileName(pluginPath)}: {ex.Message}.");
            }

            return null;
        }

        private void LoadRegisterPluginInstances(Plugin pluginInfo)
        {
            var originalPluginInstance = LoadedPlugins[pluginInfo.Name];
            var pluginInstances = _configDb.GetPluginInstancesByName(pluginInfo.Name);

            foreach (var pluginInstance in pluginInstances)
            {
                if (pluginInstance.Name.Equals(pluginInfo.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ILoadablePlugin loadablePlugin;
                try
                {
                    var type = originalPluginInstance.GetType();
                    loadablePlugin = Activator.CreateInstance(type) as ILoadablePlugin;

                    if (loadablePlugin == null)
                    {
                        _logger.Error($"Unable to create a new instance of plugin from {pluginInstance.Name}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to create an instance of plugin {pluginInstance.Name} : {ex.Message}.");
                    continue;
                }

                loadablePlugin.SetLogger(_logger);
                loadablePlugin.SetPluginConfiguration(pluginInstance.Configuration);
                loadablePlugin.AssignedCredentialType = pluginInstance.AssignedCredentialType;

                var dstPluginInstance = _configDb.SetRootPlugin(pluginInstance.Name, false);

                UpdatePluginInfo(pluginInfo, dstPluginInstance);

                _logger.Information($"Successfully configured a new instance of plugin {pluginInstance.Name}.");

                LoadedPlugins.Add(pluginInstance.Name, loadablePlugin);
            }
        }

        public Plugin DuplicatePlugin(string pluginName, bool copyConfig)
        {
            if (!IsLoadedPlugin(pluginName))
            {
                throw LogAndException($"A existing plugin with the name {pluginName} not found.");
            }

            var originalPluginInstance = LoadedPlugins[pluginName];
            ILoadablePlugin loadablePlugin;
            try
            {
                var type = originalPluginInstance.GetType();
                loadablePlugin = Activator.CreateInstance(type) as ILoadablePlugin;

                if (loadablePlugin == null)
                {
                    throw LogAndException($"Unable to create a new instance of plugin from {pluginName}");
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to create an instance of plugin {pluginName}.", ex);
            }

            _logger.Information($"Successfully created a new instance of plugin {loadablePlugin.Name}.");

            var name = Plugin.GetNewPluginInstanceName(loadablePlugin.Name);
            if (name == null)
            {
                throw LogAndException($"Unable to create a unique plugin identifier for {loadablePlugin.Name}");
            }
                    
            var displayName = loadablePlugin.DisplayName;
            var description = loadablePlugin.Description;
            loadablePlugin.SetLogger(_logger);

            var originalPluginInfo = _configDb.GetPluginByName(pluginName);
            if (originalPluginInfo == null)
            {
                throw LogAndException($"Cannot create a new instance of plugin {pluginName}. Failed to find the original plugin configuration.");
            }

            var pluginInfo = new Plugin 
            {
                Name = name,
                DisplayName = displayName,
                Description = description,
                Configuration = copyConfig ? originalPluginInfo.Configuration : loadablePlugin.GetPluginInitialConfiguration(),
                SupportedCredentialTypes = loadablePlugin.SupportedCredentialTypes,
                IsRootPlugin = false,
                Version = originalPluginInfo.Version
            };

            _configDb.SavePluginConfiguration(pluginInfo);
            _logger.Information($"Successfully configured a new instance of plugin {name}.");

            LoadedPlugins.Add(name, loadablePlugin);

            return pluginInfo;
        }

        private Plugin UpdatePluginInfo(Plugin srcPlugin, Plugin dstPlugin)
        {
            if (!string.Equals(srcPlugin.Description, dstPlugin.Description,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(srcPlugin.DisplayName, dstPlugin.DisplayName,
                    StringComparison.OrdinalIgnoreCase)
                || (srcPlugin.SupportedCredentialTypes != null && 
                    dstPlugin.SupportedCredentialTypes != null && 
                    !srcPlugin.SupportedCredentialTypes.SequenceEqual(dstPlugin.SupportedCredentialTypes))
                || !string.Equals(srcPlugin.Version, dstPlugin.Version,
                StringComparison.OrdinalIgnoreCase))
            {
                dstPlugin.DisplayName = srcPlugin.DisplayName;
                dstPlugin.Description = srcPlugin.Description;
                dstPlugin.SupportedCredentialTypes = srcPlugin.SupportedCredentialTypes;
                dstPlugin.Version = srcPlugin.Version;

                _configDb.SavePluginConfiguration(dstPlugin);
            }

            return dstPlugin;
        }

        private Plugin ConfigureIfSystemOwned(Plugin plugin)
        {
            var notLicensed = !_safeguardLogic.ValidateLicense();

            var addons = _configDb.GetAllAddons();
            foreach (var addon in addons)
            {
                if (addon.Manifest.PluginName.Equals(plugin.Name, StringComparison.OrdinalIgnoreCase))
                {
                    plugin.IsSystemOwned = addon.Manifest.IsPluginSystemOwned;
                    plugin.IsDisabled = notLicensed;
                }
            }

            return plugin;
        }

        public void RefreshPluginCredentials()
        {
            try
            {
                var sgConnection = _safeguardLogic.Connect();

                var plugins = _configDb.GetAllPlugins();

                foreach (var plugin in plugins)
                {
                    RefreshPluginCredential(sgConnection, plugin);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Failed to refresh the plugin vault credentials. If account credentials are not being synced correctly, try stopping and restarting the monitor. Reason: {ex.Message}.");
            }
        }
        
        private void RefreshPluginCredential(ISafeguardConnection sgConnection, Plugin plugin)
        {
            if (plugin.VaultAccountId.HasValue)
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
            else if (plugin.IsSystemOwned)
            {
                var addon = _configDb.GetAllAddons().FirstOrDefault(a => a.Manifest.PluginName.Equals(plugin.Name));
                if (addon != null && addon.VaultCredentials.ContainsKey(WellKnownData.DevOpsCredentialName(addon.VaultAccountName, _configDb.SvcId)))
                {
                    SendPluginVaultCredentialOnly(plugin.Name, addon.VaultCredentials[WellKnownData.DevOpsCredentialName(addon.VaultAccountName, _configDb.SvcId)]);
                }
            }
        }

        public bool IsLoadedPlugin(string name)
        {
            return (LoadedPlugins.ContainsKey(name));
        }

        public void Dispose()
        {
            _watcher.Created -= OnDirectoryCreate;
            _watcher.Dispose();
        }
    }
}
