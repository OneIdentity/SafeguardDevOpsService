
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.DevOps.Logic;
using OneIdentity.DevOps.Plugins;
using OneIdentity.SafeguardDotNet;
using OneIdentity.SafeguardDotNet.A2A;
using OneIdentity.SafeguardDotNet.Event;


namespace OneIdentity.DevOps.Logic
{
    internal class ConfigurationLogic : IConfigurationLogic
    {
        private readonly int _safeguardApiVersion = 3;
        private readonly bool _safeguardIgnoreSsl = true;
        private readonly Serilog.ILogger _logger;


        private readonly IConfigurationRepository _configurationRepository;
        private readonly IPluginManager _pluginManager;

        public ConfigurationLogic(IConfigurationRepository configurationRepository, IPluginManager pluginManager)
        {
            _configurationRepository = configurationRepository;
            _pluginManager = pluginManager;
            _logger = Serilog.Log.Logger;
        }

        public Configuration InitialConfiguration(InitialConfiguration initialConfig)
        {
            //TODO: Create a new configuration element here
            //TODO: Check to see if there is already a configuration.  If so, throw.
            //TODO: Get the registration and store the configuration in the database

            if (initialConfig == null)
                throw new DevOpsException("The initial configuration cannot be null.");
            if (initialConfig.CertificateUserThumbprint == null)
                throw new DevOpsException("The user certificate thumbprint cannot be null.");
            if (initialConfig.SppAddress == null)
                throw new DevOpsException("The SPP network address cannot be null.");

            ISafeguardConnection connection = null;
            try
            {

                connection = Safeguard.Connect(initialConfig.SppAddress, initialConfig.CertificateUserThumbprint,
                    _safeguardApiVersion, _safeguardIgnoreSsl);

                var rawJson = connection.InvokeMethod(Service.Core, Method.Get, "A2ARegistrations");

                var registrations = JsonHelper.DeserializeObject<IEnumerable<SppRegistration>>(rawJson);

                // TODO: Assume that we only have one registration that belongs to the cert user
                var registration = registrations?.FirstOrDefault();
                if (registration != null)
                {
                    var configuration = new Configuration
                    {
                        SppAddress = initialConfig.SppAddress,
                        A2ARegistrationId = registration.Id,
                        A2ARegistrationName = registration.AppName,
                        CertificateUser = registration.CertificateUser,
                        CertificateUserThumbPrint = registration.CertificateUserThumbPrint,
                        CreatedByUserId = registration.CreatedByUserId,
                        CreatedByUserDisplayName = registration.CreatedByUserDisplayName,
                        CreatedDate = registration.CreatedDate,
                        AccountMapping = new List<AccountMapping>()
                    };

                    _configurationRepository.SaveConfiguration(configuration);
                    return configuration;
                }
                else
                {
                    _logger.Error("No A2A registrations were found for the configured certificate user");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to initialize the DevOps Serivce: {ex.Message}");
            }

            finally
            {
                connection?.Dispose();
            }

            throw new DevOpsException("Failed to configure devops.");
        }

        public void DeleteConfiguration()
        {
            _configurationRepository.DeleteConfiguration();
        }

        public Registration GetRegistration()
        {
            return JsonHelper.DeserializeObject<Registration>(JsonHelper.SerializeObject(_configurationRepository.GetConfiguration()));
        }

        public Configuration UpdateConnectionConfiguration(ConnectionConfiguration connectionConfig)
        {
            if (connectionConfig == null)
                throw new DevOpsException("The initial configuration cannot be null.");
            if (connectionConfig.CertificateUserThumbprint == null)
                throw new DevOpsException("The user certificate thumbprint cannot be null.");
            if (connectionConfig.SppAddress == null)
                throw new DevOpsException("The SPS network address cannot be null.");

            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null)
            {
                _logger.Error("No configuration was found.  DevOps service must be configured first");
                return null;
            }

            configuration.CertificateUserThumbPrint = connectionConfig.CertificateUserThumbprint;
            configuration.SppAddress = connectionConfig.SppAddress;

            //Validate the connection information
            var connection = Safeguard.Connect(connectionConfig.SppAddress,
                connectionConfig.CertificateUserThumbprint, _safeguardApiVersion, _safeguardIgnoreSsl);
            if(connection == null)
                _logger.Error("SPP connection configuration failed.");

            connection?.LogOut();

            _configurationRepository.SaveConfiguration(configuration);

            return configuration;
        }

        public IEnumerable<AccountMapping> GetAccountMappings(string accountName = "", string vaultName = "")
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null)
            {
                _logger.Error("No configuration was found.  DevOps service must be configured first");
                return null;
            }

            if (String.IsNullOrEmpty(accountName) && String.IsNullOrEmpty(vaultName))
                return configuration.AccountMapping.ToArray();

            var accountMappings = configuration.AccountMapping.Where(x =>
                x.AccountName.StartsWith(accountName ?? throw new ArgumentNullException(nameof(accountName))) || x.VaultName.StartsWith(vaultName));
            return accountMappings;
        }

        public IEnumerable<AccountMapping> SaveAccountMappings(IEnumerable<AccountMapping> newAccountMappings)
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null)
            {
                _logger.Error("No configuration was found.  DevOps service must be configured first");
                return null;
            }

            var accountMappingList = configuration.AccountMapping.ToList();
            var newAccountMappingsList = newAccountMappings.ToList();

            accountMappingList.AddRange(newAccountMappingsList.Where(p2 => accountMappingList.All(p1 => !p1.Equals(p2))));
            configuration.AccountMapping = accountMappingList;

            _configurationRepository.SaveConfiguration(configuration);
            return accountMappingList;
        }

        public IEnumerable<AccountMapping> RemoveAccountMappings(bool removeAll, string accountName, string vaultName)
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null)
            {
                _logger.Error("No configuration was found.  DevOps service must be configured first");
                return null;
            }

            if (removeAll && accountName == null && vaultName == null)
            {
                configuration.AccountMapping = new List<AccountMapping>();
            }
            else
            {
                var accountMappingList = configuration.AccountMapping.ToList();
                if (accountName != null && vaultName != null)
                {
                    accountMappingList.RemoveAll(x => x.AccountName == accountName && x.VaultName == vaultName);
                } else if (accountName != null)
                {
                    accountMappingList.RemoveAll(x => x.AccountName == accountName);
                }
                else
                {
                    accountMappingList.RemoveAll(x => x.VaultName == vaultName);
                }

                configuration.AccountMapping = accountMappingList;
            }

            _configurationRepository.SaveConfiguration(configuration);

            return configuration.AccountMapping;
        }

        public IEnumerable<RetrievableAccount> GetRetrievableAccounts()
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null)
            {
                _logger.Error("No configuration was found.  DevOps service must be configured first");
                return null;
            }

            ISafeguardConnection connection = null;
            try
            {
                connection = Safeguard.Connect(configuration.SppAddress, configuration.CertificateUserThumbPrint,
                    _safeguardApiVersion, _safeguardIgnoreSsl);
                var rawJson = connection.InvokeMethod(Service.Core, Method.Get,
                    $"A2ARegistrations/{configuration.A2ARegistrationId}/RetrievableAccounts");
                var retrievableAccounts = JsonHelper.DeserializeObject<IEnumerable<RetrievableAccount>>(rawJson);

                return retrievableAccounts.ToList();
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to get the retrievable accounts from SPP: {ex.Message}.");
            }
            finally
            {
                connection?.Dispose();
            }

            return null;
        }

        public void EnableMonitoring(bool enable)
        {
            if (enable)
                StartMonitoring();
            else
                StopMonitoring();
        }

        public IEnumerable<Plugin> GetAllPlugins()
        {
            return _configurationRepository.GetAllPlugins();
        }

        public Plugin GetPluginByName(string name)
        {
            return _configurationRepository.GetPluginByName(name);
        }

        public void DeletePluginByName(string name)
        {
            _configurationRepository.DeletePluginByName(name);
        }


        public Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name)
        {
            var plugin = _configurationRepository.GetPluginByName(name);

            if (plugin == null)
            {
                _logger.Error($"Failed to save the configuration. No plugin {name} was found.");
                return null;
            }

            plugin.Configuration = pluginConfiguration.Configuration;
            plugin = _configurationRepository.SavePluginConfiguration(plugin);
            _pluginManager.SetConfigurationForPlugin(name);

            return plugin;
        }

        private IEnumerable<AccountMapping> GetAccountMappings(Configuration configuration)
        {
            ISafeguardConnection connection = null;
            try
            {
                connection = Safeguard.Connect(configuration.SppAddress, configuration.CertificateUserThumbPrint,
                    _safeguardApiVersion, _safeguardIgnoreSsl);
                var rawJson = connection.InvokeMethod(Service.Core, Method.Get,
                    $"A2ARegistrations/{configuration.A2ARegistrationId}/RetrievableAccounts");

                var retrievableAccounts = JsonHelper.DeserializeObject<IEnumerable<RetrievableAccount>>(rawJson);

                var accountMappings = new List<AccountMapping>();
                foreach (var account in retrievableAccounts)
                {
                    accountMappings.Add(new AccountMapping()
                    {
                        AccountName = account.AccountName,
                        ApiKey = account.ApiKey,
                        VaultName = ""
                    });
                }

                return accountMappings;
            }
            finally
            {
                connection?.Dispose();
            }
        }

        private RetrievableAccount GetRetrievableAccount(Configuration configuration, string apiKey)
        {
            var apiKeyInfo = _configurationRepository.GetSetting(apiKey);

            ISafeguardConnection connection = null;
            try
            {

                connection = Safeguard.Connect(configuration.SppAddress, configuration.CertificateUserThumbPrint,
                    _safeguardApiVersion, _safeguardIgnoreSsl);
                var rawJson = connection.InvokeMethod(Service.Core, Method.Get,
                    $"A2ARegistrations/{configuration.A2ARegistrationId}/RetrievableAccounts/{apiKeyInfo.Value}");

                var retrievableAccount = JsonHelper.DeserializeObject<IEnumerable<RetrievableAccount>>(rawJson);

                return retrievableAccount?.FirstOrDefault();
            }
            finally
            {
                connection?.Dispose();
            }
        }

        private void SaveRetrievableAccount(Configuration configuration, RetrievableAccount retrievableAccount)
        {
            var apiKeyInfo = new Setting()
            {
                Name = retrievableAccount.ApiKey,
                Value = retrievableAccount.AccountId.ToString()
            };

            _configurationRepository.SetSetting(apiKeyInfo);
        }

        private void DeleteRetrievableAccount(Configuration configuration, string apiKey)
        {
            _configurationRepository.RemoveSetting(apiKey);
        }

        private static ISafeguardEventListener _eventListener;
        private static ISafeguardA2AContext _a2AContext;
        private static List<RetrievableAccount> _retrievableAccounts;

        private void StartMonitoring()
        {
            if (_eventListener != null)
                throw new DevOpsException("Listener is already running.");

            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null)
            {
                _logger.Error("No configuration was found.  DevOps service must be configured first");
                return;
            }

            // connect to Safeguard
            _a2AContext = Safeguard.A2A.GetContext(configuration.SppAddress, configuration.CertificateUserThumbPrint,
                _safeguardApiVersion, _safeguardIgnoreSsl);

            // figure out what API keys to monitor
            _retrievableAccounts = GetRetrievableAccounts().ToList();
            if (_retrievableAccounts.Count == 0)
            {
                _logger.Error("No API keys found in A2A registrations.  Nothing to do.");
                throw new DevOpsException("No API keys found in A2A registrations.  Nothing to do.");
            }

            var apiKeys = new List<SecureString>();
            foreach (var account in _retrievableAccounts)
            {
                apiKeys.Add(account.ApiKey.ToSecureString());
            }

            _eventListener = _a2AContext.GetPersistentA2AEventListener(apiKeys, PasswordChangeHandler);
            _eventListener.Start();

            _logger.Information("Password change monitoring has been started.");
        }

        private void StopMonitoring()
        {
            try
            {
                _eventListener?.Stop();
                _a2AContext?.Dispose();
                _logger.Information("Password change monitoring has been stopped.");
            }
            finally
            {
                _eventListener = null;
                _a2AContext = null;
                _retrievableAccounts = null;
            }
        }

        private void PasswordChangeHandler(string eventName, string eventBody)
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null || _retrievableAccounts == null)
            {
                _logger.Error("No configuration was found.  DevOps service must be configured first or no retrievable accounts found.");
                return;
            }

            var eventInfo = JsonHelper.DeserializeObject<EventInfo>(eventBody);

            try
            {
                var apiKey = _retrievableAccounts.Single(mp => mp.SystemName == eventInfo.AssetName && mp.AccountName == eventInfo.AccountName).ApiKey;
                using (var password = _a2AContext.RetrievePassword(apiKey.ToSecureString()))
                {
                    var accounts = configuration.AccountMapping.ToList();
                    var selectedAccounts = accounts.Where(a => a.ApiKey.Equals(apiKey));
                    foreach (var account in selectedAccounts)
                    {
                        try
                        {
                            _logger.Information($"Sending password for account {account.AccountName} to {account.VaultName}.");
                            if (!_pluginManager.SendPassword(account.VaultName, account.AccountName, password))
                                _logger.Error(
                                    $"Unable to set the password for {account.AccountName} to {account.VaultName}.");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                $"Unable to set the password for {account.AccountName} to {account.VaultName}: {ex.Message}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Password change handler failed: {ex.Message}.");
            }
        }
    }
}
