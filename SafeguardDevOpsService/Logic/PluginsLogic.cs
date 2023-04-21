using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.SafeguardDotNet;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;

namespace OneIdentity.DevOps.Logic
{
    internal class PluginsLogic : IPluginsLogic
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private readonly IPluginManager _pluginManager;
        private readonly ISafeguardLogic _safeguardLogic;

        public PluginsLogic(IConfigurationRepository configDb, IPluginManager pluginManager, ISafeguardLogic safeguardLogic)
        {
            _configDb = configDb;
            _pluginManager = pluginManager;
            _safeguardLogic = safeguardLogic;
            _logger = Serilog.Log.Logger;
        }

        private DevOpsException LogAndException(string msg, Exception ex = null)
        {
            _logger.Error(msg);
            return new DevOpsException(msg, ex);
        }

        public IEnumerable<Plugin> GetAllPlugins(bool includeDeleted = false)
        {
            var plugins = _configDb.GetAllPlugins().ToList();
            plugins.ForEach(x => x.IsLoaded = _pluginManager.IsLoadedPlugin(x.Name));
            plugins.ForEach(x => x.MappedAccountsCount = GetAccountMappingsCount(x.Name));

            if (includeDeleted)
                return plugins;

            return plugins.Where(x => x.IsDeleted == false).ToList();
        }

        public IEnumerable<Plugin> GetAllPluginInstancesByName(string name, bool includeDeleted = false)
        {
            var plugins = _configDb.GetPluginInstancesByName(name).ToList();
            plugins.ForEach(x => x.IsLoaded = _pluginManager.IsLoadedPlugin(x.Name));
            plugins.ForEach(x => x.MappedAccountsCount = GetAccountMappingsCount(x.Name));

            if (includeDeleted)
                return plugins;

            return plugins.Where(x => x.IsDeleted == false).ToList();
        }

        private void InstallPlugin(ZipArchive zipArchive)
        {
            var manifestEntry = zipArchive.GetEntry(WellKnownData.ManifestPattern);
            if (manifestEntry == null)
            {
                throw LogAndException("Failed to find the manifest for the vault plugin.");
            }

            using (var reader = new StreamReader(manifestEntry.Open()))
            {
                var manifest = reader.ReadToEnd();
                var pluginManifest = JsonHelper.DeserializeObject<PluginManifest>(manifest);
                if (pluginManifest != null && ValidateManifest(pluginManifest))
                {
                    var extractLocation = Path.Combine(WellKnownData.PluginDirPath, pluginManifest.Name);
                    if (_pluginManager.IsLoadedPlugin(pluginManifest.Name) || Directory.Exists(extractLocation))
                    {
                        _logger.Debug("Plugin is already loaded, setting restart flag.");
                        RestartManager.Instance.ShouldRestart = true;

                        if (!Directory.Exists(WellKnownData.PluginStageDirPath))
                            Directory.CreateDirectory(WellKnownData.PluginStageDirPath);
                        extractLocation = Path.Combine(WellKnownData.PluginStageDirPath, pluginManifest.Name);
                    }
                    _logger.Debug($"Extracting plugin ZIP to {extractLocation}.");
                    zipArchive.ExtractToDirectory(extractLocation, true);
                }
                else
                {
                    throw LogAndException($"Plugin package does not contain a valid {WellKnownData.ManifestPattern} file.");
                }
            }
        }

        private bool ValidateManifest(PluginManifest pluginManifest)
        {
            return pluginManifest != null 
                   && pluginManifest.GetType().GetProperties()
                       .Where(pi => pi.PropertyType == typeof(string))
                       .Select(pi => (string) pi.GetValue(pluginManifest))
                       .All(value => !string.IsNullOrEmpty(value)) 
                   && pluginManifest.Type.Equals(WellKnownData.PluginUploadType, StringComparison.OrdinalIgnoreCase);
        }

        public void InstallPlugin(IFormFile formFile)
        {
            if (formFile.Length <= 0)
                throw LogAndException("Plugin cannot be null or empty");

            try
            {
                using (var inputStream = formFile.OpenReadStream())
                using (var zipArchive = new ZipArchive(inputStream, ZipArchiveMode.Read))
                {
                    InstallPlugin(zipArchive);
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to install the vault plugin. {ex.Message}");
            }
        }

        public void InstallPlugin(string base64Plugin)
        {
            if (base64Plugin == null)
                throw LogAndException("Plugin cannot be null");

            var bytes = Convert.FromBase64String(base64Plugin);

            try
            {
                using (var inputStream = new MemoryStream(bytes))
                using (var zipArchive = new ZipArchive(inputStream, ZipArchiveMode.Read))
                {
                    InstallPlugin(zipArchive);
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to install the vault plugin. {ex.Message}");
            }
        }

        public Plugin CreatePluginInstanceByName(string name, bool copyConfig)
        {
            return _pluginManager.DuplicatePlugin(name, copyConfig);
        }

        public Plugin GetPluginByName(string name)
        {
            var plugin = _configDb.GetPluginByName(name);
            if (plugin != null)
            {
                plugin.IsLoaded = _pluginManager.IsLoadedPlugin(plugin.Name);
                plugin.MappedAccountsCount = GetAccountMappingsCount(plugin.Name);
            }

            return plugin;
        }

        public void DeletePluginByName(string name)
        {
            var pluginInfo = _configDb.GetPluginByName(name);
            if (pluginInfo != null)
            {
                // If the plugin is not the root plugin, then hard delete it and move on.
                // If it is the root plugin, then don't actually delete the plugin configuration yet.
                // Mark the plugin to be deleted and then delete it on the next restart.
                if (_configDb.DeletePluginByName(name, !pluginInfo.IsRootPlugin) && pluginInfo.IsRootPlugin)
                {
                    RestartManager.Instance.ShouldRestart = true;
                }
            }
        }

        public void DeleteAllPluginInstancesByName(string name)
        {
            var pluginInstances = _configDb.GetPluginInstancesByName(name);
            foreach (var pluginInstance in pluginInstances)
            {
                DeleteAccountMappings(pluginInstance.Name);
                RemovePluginVaultAccount(pluginInstance.Name);

                if (pluginInstance.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    // Just soft delete the original plugin.
                    _configDb.DeletePluginByName(pluginInstance.Name);
                    continue;
                }

                // Hard delete all of the other instances.
                _configDb.DeletePluginByName(pluginInstance.Name, true);
            }

            RestartManager.Instance.ShouldRestart = true;
        }


        public Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name)
        {
            var plugin = _configDb.GetPluginByName(name);

            if (plugin == null)
            {
                var msg = $"Failed to save the safeguardConnection. No plugin {name} was found.";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.NotFound);
            }

            if (plugin.IsSystemOwned)
            {
                var msg = $"Failed to save the safeguardConnection. The plugin {name} is system owned.";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.BadRequest);
            }

            if (pluginConfiguration.Configuration != null)
            {
                plugin.Configuration = pluginConfiguration.Configuration;
            }
            if (pluginConfiguration.AssignedCredentialType != CredentialType.Unknown)
            {
                plugin.AssignedCredentialType = pluginConfiguration.AssignedCredentialType;
            }

            plugin = _configDb.SavePluginConfiguration(plugin);
            plugin.IsLoaded = _pluginManager.IsLoadedPlugin(plugin.Name);
            _pluginManager.SetConfigurationForPlugin(name);

            return plugin;
        }

        public bool TestPluginConnectionByName(ISafeguardConnection sgConnection, string name)
        {
            var plugin = _configDb.GetPluginByName(name);

            if (plugin == null)
            {
                var msg = $"Failed to test the safeguardConnection. No plugin {name} was found.";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.NotFound);
            }

            return _pluginManager.TestPluginVaultConnection(sgConnection, name);
        }

        public PluginState GetPluginDisabledState(string name)
        {
            var plugin = _configDb.GetPluginByName(name);

            if (plugin == null)
            {
                var msg = $"Plugin {name} not found";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.NotFound);
            }

            return new PluginState() {Disabled = plugin.IsDisabled};
        }

        public PluginState UpdatePluginDisabledState(string name, bool state)
        {
            var plugin = _configDb.GetPluginByName(name);

            if (plugin == null)
            {
                var msg = $"Plugin {name} not found";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.NotFound);
            }

            plugin.IsDisabled = state;
            _configDb.SavePluginConfiguration(plugin);

            return new PluginState() {Disabled = plugin.IsDisabled};
        }

        public IEnumerable<AccountMapping> GetAccountMappings(string name, bool includeAllInstances = false)
        {
            var plugin = _configDb.GetPluginByName(name);
            if (plugin == null)
            {
                var msg = $"Plugin {name} not found";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.NotFound);
            }

            var mappings = _configDb.GetAccountMappings();

            var accountMappings = includeAllInstances ? mappings.Where(x => x.VaultName.StartsWith(plugin.RootPluginName, StringComparison.InvariantCultureIgnoreCase)) :
                mappings.Where(x => x.VaultName.Equals(name, StringComparison.InvariantCultureIgnoreCase));

            return accountMappings;
        }

        private int GetAccountMappingsCount(string name)
        {
            try
            {
                var mappings = GetAccountMappings(name);
                return mappings?.Count() ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public AccountMapping GetAccountMappingById(string name, int accountId)
        {
            if (_configDb.GetPluginByName(name) == null)
            {
                var msg = $"Plugin {name} not found";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.NotFound);
            }

            var mappings = _configDb.GetAccountMappings();

            var accountMappings = mappings.Where(x => x.VaultName.Equals(name, StringComparison.InvariantCultureIgnoreCase) && x.AccountId == accountId).ToArray();
            if (accountMappings.Length > 0)
            {
                return accountMappings.FirstOrDefault();
            }

            return null;
        }

        public IEnumerable<AccountMapping> SaveAccountMappings(ISafeguardConnection sgConnection, string name, IEnumerable<A2ARetrievableAccount> accounts)
        {
            if (_configDb.A2aRegistrationId == null)
            {
                var msg = "A2A registration not configured";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }

            if (_configDb.GetPluginByName(name) == null)
            {
                var msg = $"Plugin {name} not found";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.NotFound);
            }

            var retrievableAccounts = accounts.ToArray();
            if (retrievableAccounts.All(x => x.AccountId == 0))
            {
                var msg = "Invalid list of accounts. Expecting a list of retrievable accounts.";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }

            var allAccounts = _configDb.GetAccountMappings().ToArray();
            foreach (var account in retrievableAccounts)
            {
                if (!string.IsNullOrEmpty(account.AltAccountName))
                {
                    // Make sure that no other existing account has the same altAccountName
                    // Make sure that none of the accounts that are being added, have the same altAccountName
                    if (allAccounts.Any(x => x.AltAccountName != null 
                                             && x.AltAccountName.Equals(account.AltAccountName, StringComparison.OrdinalIgnoreCase)
                                             && !x.VaultName.Equals(name, StringComparison.OrdinalIgnoreCase)) 
                        || retrievableAccounts.Any(x => x.AccountId != account.AccountId 
                                                        && x.AltAccountName != null 
                                                        && x.AltAccountName.Equals(account.AltAccountName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var msg = $"Invalid alternate account name. The account name {account.AltAccountName} is already in use.";
                        _logger.Error(msg);
                        throw new DevOpsException(msg);
                    }
                }
            }

            var sg = sgConnection ?? _safeguardLogic.Connect();

            try
            {
                var newAccounts = new List<AccountMapping>();

                foreach (var account in retrievableAccounts)
                {
                    try
                    {
                        var retrievableAccount =
                            _safeguardLogic.GetA2ARetrievableAccountById(sg, A2ARegistrationType.Account, account.AccountId);

                        if (retrievableAccount != null)
                        {
                            var accountMapping = new AccountMapping()
                            {
                                AccountName = retrievableAccount.AccountName,
                                AltAccountName = account.AltAccountName,
                                AccountId = retrievableAccount.AccountId,
                                ApiKey = retrievableAccount.ApiKey,
                                AssetName = retrievableAccount.SystemName,
                                SystemId = retrievableAccount.SystemId,
                                DomainName = retrievableAccount.DomainName,
                                NetworkAddress = retrievableAccount.NetworkAddress,
                                VaultName = name
                            };

                            newAccounts.Add(accountMapping);
                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Failed to add account {account.AccountId} - {account.AccountName}: {ex.Message}";
                        _logger.Error(ex, msg);
                    }
                }

                if (newAccounts.Count > 0)
                {
                    _configDb.SaveAccountMappings(newAccounts);
                }

                return GetAccountMappings(name);
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }
        }

        public void DeleteAccountMappings(string name)
        {
            var mappings = GetAccountMappings(name);

            foreach (var account in mappings)
            {
                try
                {
                    _configDb.DeleteAccountMappingsByKey(account.Key);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to remove the account mapping {account.AssetName}-{account.AccountName} from plugin {name}.", ex);
                }
            }
        }

        public void DeleteAccountMappings(string name, IEnumerable<AccountMapping> mappings)
        {
            if (mappings == null)
            {
                throw LogAndException("A list of accounts mappings must be provided.");
            }

            foreach (var account in mappings)
            {
                // Skip any mapping that doesn't have the matching vault name.
                if (account.VaultName == null || !account.VaultName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                try
                {
                    _configDb.DeleteAccountMappingsByKey(account.Key);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to remove the account mapping {account.AssetName}-{account.AccountName} from plugin {name}.", ex);
                }
            }
        }

        public void DeleteAccountMappings()
        {
            _configDb.DeleteAccountMappings();
        }

        public A2ARetrievableAccount GetPluginVaultAccount(ISafeguardConnection sgConnection, string name)
        {
            var plugin = _configDb.GetPluginByName(name);
            if (plugin == null)
            {
                throw LogAndException($"Plugin {name} not found");
            }
            if (plugin.IsSystemOwned && !plugin.VaultAccountId.HasValue)
            {
                var account = new A2ARetrievableAccount()
                {
                    AccountName = WellKnownData.DevOpsCredentialName(plugin.Name, _configDb.SvcId),
                    AccountDescription = "Internal account",
                    SystemName = WellKnownData.DevOpsAssetName(_configDb.SvcId),
                    SystemDescription = WellKnownData.DevOpsAssetName(_configDb.SvcId)
                };

                var addon = _configDb.GetAllAddons().FirstOrDefault(a => a.Manifest.PluginName.Equals(plugin.Name));
                if (addon != null)
                {
                    account.AccountName = addon.VaultAccountName;
                    account.SystemName = addon.Name;
                    account.SystemDescription = addon.Name;
                }

                return account;
            }
            if (!plugin.VaultAccountId.HasValue)
            {
                return null;
            }

            return _safeguardLogic.GetA2ARetrievableAccount(sgConnection, plugin.VaultAccountId.Value, A2ARegistrationType.Vault);
        }

        private int VaultAccountUsage(int accountId)
        {
            var plugins = _configDb.GetAllPlugins();
            var usage = plugins.Where(x => x.VaultAccountId == accountId).ToArray();
            return usage.Length;
        }

        public A2ARetrievableAccount SavePluginVaultAccount(ISafeguardConnection sgConnection, string name, AssetAccount sppAccount)
        {
            var plugin = _configDb.GetPluginByName(name);
            if (plugin == null)
            {
                throw LogAndException($"Plugin {name} not found.");
            }
            if (sppAccount == null)
            {
                throw LogAndException("Invalid account.");
            }

            var account = _safeguardLogic.GetAssetAccount(sgConnection, sppAccount.Id);
            if (account == null)
            {
                throw LogAndException($"Account {sppAccount.Id} not found.");
            }

            // Make sure that the vault account isn't being used by another plugin before we delete it.
            if (plugin.VaultAccountId != null && (VaultAccountUsage(plugin.VaultAccountId.Value) <= 1))
            {
                _safeguardLogic.DeleteA2ARetrievableAccount(sgConnection, plugin.VaultAccountId.Value, A2ARegistrationType.Vault);
            }

            var accounts = _safeguardLogic.AddA2ARetrievableAccounts(sgConnection, new List<SppAccount>() {new SppAccount() {Id = account.Id, Name = account.Name}}, A2ARegistrationType.Vault);

            var a2aAccount = accounts.FirstOrDefault(x => x.AccountId == account.Id);
            if (a2aAccount != null)
            {
                plugin.VaultAccountId = a2aAccount.AccountId;
                _configDb.SavePluginConfiguration(plugin);
            }
            else
            {
                throw LogAndException($"Failed to add the account to the A2A vault registration.  {account.Id} - {account.Name}.");
            }

            return a2aAccount;
        }

        public void RemovePluginVaultAccount(string name)
        {
            var plugin = _configDb.GetPluginByName(name);
            if (plugin == null)
            {
                throw LogAndException($"Plugin {name} not found.");
            }

            // Make sure that the vault account isn't being used by another plugin before we delete it.
            if (plugin.VaultAccountId != null && (VaultAccountUsage(plugin.VaultAccountId.Value) <= 1))
            {
                _safeguardLogic.DeleteA2ARetrievableAccount(null, plugin.VaultAccountId.Value, A2ARegistrationType.Vault);
            }

            plugin.VaultAccountId = null;
            _configDb.SavePluginConfiguration(plugin);
        }

        // This method just removes the vault account mappings in the local database.  It does
        //  not remove the vault A2A registration.
        public void ClearMappedPluginVaultAccounts()
        {
            var plugins = _configDb.GetAllPlugins();
            foreach (var plugin in plugins)
            {
                plugin.VaultAccountId = null;
                _configDb.SavePluginConfiguration(plugin);
            }
        }

        public void RestartService()
        {
            _safeguardLogic.RestartService();
        }
    }
}
