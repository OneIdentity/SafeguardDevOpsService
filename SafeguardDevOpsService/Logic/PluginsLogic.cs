using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
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

        private DevOpsException LogAndThrow(string msg, Exception ex = null)
        {
            _logger.Error(msg);
            return new DevOpsException(msg, ex);
        }

        public IEnumerable<Plugin> GetAllPlugins()
        {
            var plugins = _configDb.GetAllPlugins().ToList();
            plugins.ForEach(x => x.IsLoaded = _pluginManager.IsLoadedPlugin(x.Name));
            return plugins;
        }

        private void InstallPlugin(ZipArchive zipArchive)
        {
            var manifestEntry = zipArchive.GetEntry(WellKnownData.ManifestPattern);
            if (manifestEntry == null)
            {
                throw LogAndThrow("Failed to find the manifest for the vault plugin.");
            }

            using (var reader = new StreamReader(manifestEntry.Open()))
            {
                var manifest = reader.ReadToEnd();
                var pluginManifest = JsonHelper.DeserializeObject<PluginManifest>(manifest);
                if (pluginManifest != null)
                {
                    var extractLocation = Path.Combine(WellKnownData.PluginDirPath, pluginManifest.Name);
                    if (_pluginManager.IsLoadedPlugin(pluginManifest.Name))
                    {
                        RestartManager.Instance.ShouldRestart = true;

                        if (!Directory.Exists(WellKnownData.PluginStageDirPath))
                            Directory.CreateDirectory(WellKnownData.PluginStageDirPath);
                        extractLocation = Path.Combine(WellKnownData.PluginStageDirPath, pluginManifest.Name);
                    }
                    zipArchive.ExtractToDirectory(extractLocation, true);
                }
                else
                {
                    throw LogAndThrow($"Plugin package does not contain a {WellKnownData.ManifestPattern} file.");
                }
            }
        }
        public void InstallPlugin(IFormFile formFile)
        {
            if (formFile.Length <= 0)
                throw LogAndThrow("Plugin cannot be null or empty");

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
                throw LogAndThrow($"Failed to install the vault plugin. {ex.Message}");
            }
        }

        public void InstallPlugin(string base64Plugin)
        {
            if (base64Plugin == null)
                throw LogAndThrow("Plugin cannot be null");

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
                throw LogAndThrow($"Failed to install the vault plugin. {ex.Message}");
            }
        }

        public Plugin GetPluginByName(string name)
        {
            var plugin = _configDb.GetPluginByName(name);
            if (plugin != null)
            {
                plugin.IsLoaded = _pluginManager.IsLoadedPlugin(plugin.Name);
            }

            return plugin;
        }

        public void DeletePluginByName(string name)
        {
            _configDb.DeletePluginByName(name);
        }


        public Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name)
        {
            var plugin = _configDb.GetPluginByName(name);

            if (plugin == null)
            {
                _logger.Error($"Failed to save the safeguardConnection. No plugin {name} was found.");
                return null;
            }

            plugin.Configuration = pluginConfiguration.Configuration;
            plugin = _configDb.SavePluginConfiguration(plugin);
            _pluginManager.SetConfigurationForPlugin(name);

            return plugin;
        }

        public IEnumerable<AccountMapping> GetAccountMappings(string name)
        {
            if (_configDb.GetPluginByName(name) == null)
            {
                var msg = $"Plugin {name} not found";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }

            var mappings = _configDb.GetAccountMappings();

            var accountMappings = mappings.Where(x => x.VaultName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            return accountMappings;
        }

        public IEnumerable<AccountMapping> SaveAccountMappings(string name, IEnumerable<A2ARetrievableAccount> accounts)
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
                throw new DevOpsException(msg);
            }

            using var sg = _safeguardLogic.Connect();

            var newAccounts = new List<AccountMapping>();

            foreach (var account in accounts)
            {
                try
                {
                    var result = sg.InvokeMethodFull(Service.Core, Method.Get, $"A2ARegistrations/{_configDb.A2aRegistrationId}/RetrievableAccounts/{account.AccountId}");
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        var retrievableAccount = JsonHelper.DeserializeObject<A2ARetrievableAccount>(result.Body);
                        var accountMapping = new AccountMapping()
                        {
                            AccountName = retrievableAccount.AccountName,
                            ApiKey = retrievableAccount.ApiKey,
                            AssetName = retrievableAccount.SystemName,
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
                    _logger.Error(msg);
                }
            }

            if (newAccounts.Count > 0)
            {
                _configDb.SaveAccountMappings(newAccounts);
            }

            return GetAccountMappings(name);
        }

        public void DeleteAccountMappings(string name)
        {
            var mappings = GetAccountMappings(name);

            foreach (var account in mappings)
            {
                _configDb.DeleteAccountMappingsByKey(account.Key);
            }
        }

        public void DeleteAccountMappings()
        {
            _configDb.DeleteAccountMappings();
        }

        public A2ARetrievableAccount GetPluginVaultAccount(string name)
        {
            var plugin = _configDb.GetPluginByName(name);
            if (plugin == null)
            {
                LogAndThrow($"Plugin {name} not found");
            }
            if (!plugin.VaultAccountId.HasValue)
            {
                LogAndThrow($"Plugin {name} is not associated with an account");
            }

            return _safeguardLogic.GetA2ARetrievableAccount(plugin.VaultAccountId.Value, A2ARegistrationType.Vault);
        }

        public A2ARetrievableAccount SavePluginVaultAccount(string name, AssetAccount sppAccount)
        {
            var plugin = _configDb.GetPluginByName(name);
            if (plugin == null)
            {
                LogAndThrow($"Plugin {name} not found.");
            }
            if (sppAccount == null)
            {
                LogAndThrow("Invalid account.");
            }

            var account = _safeguardLogic.GetAccount(sppAccount.Id);
            if (account == null)
            {
                LogAndThrow($"Account {sppAccount.Id} not found.");
            }

            if (plugin.VaultAccountId != null)
            {
                _safeguardLogic.DeleteA2ARetrievableAccount(plugin.VaultAccountId.Value, A2ARegistrationType.Vault);
            }

            var accounts = _safeguardLogic.AddA2ARetrievableAccounts(new List<SppAccount>() {new SppAccount() {Id = account.Id, Name = account.Name}}, A2ARegistrationType.Vault);

            var a2aAccount = accounts.FirstOrDefault(x => x.AccountId == account.Id);
            if (a2aAccount != null)
            {
                plugin.VaultAccountId = a2aAccount.AccountId;
                // plugin.ApiKey = a2aAccount.ApiKey;
                _configDb.SavePluginConfiguration(plugin);
            }
            else
            {
                LogAndThrow($"Failed to add the account to the A2A vault registration.  {account.Id} - {account.Name}.");
            }

            return a2aAccount;
        }

        public void RestartService()
        {
            _safeguardLogic.RestartService();
        }
    }
}
