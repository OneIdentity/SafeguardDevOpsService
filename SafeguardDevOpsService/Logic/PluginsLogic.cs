using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
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
            return _configDb.GetAllPlugins();
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
                    var manifestEntry = zipArchive.GetEntry("Manifest.json");
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
                            _pluginManager.UnloadPlugin(pluginManifest.Name);
                        }
                    }

                    //TODO: Figure out how to unload all of the plugins before installing a new one.
                    zipArchive.ExtractToDirectory(WellKnownData.PluginDirPath, true);
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

            using (var inputStream = new MemoryStream(bytes))
            using (var zipArchive = new ZipArchive(inputStream, ZipArchiveMode.Read))
            {
                zipArchive.ExtractToDirectory(WellKnownData.PluginDirPath);
            }
        }

        public Plugin GetPluginByName(string name)
        {
            return _configDb.GetPluginByName(name);
        }

        public void DeletePluginByName(string name)
        {
            _pluginManager.UnloadPlugin(name);
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

    }
}
