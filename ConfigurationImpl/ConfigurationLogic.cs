
using System;
using System.Collections.Generic;
using System.Linq;
using OneIdentity.SafeguardDevOpsService.ConfigDb;
using OneIdentity.SafeguardDevOpsService.Data;
using OneIdentity.SafeguardDevOpsService.Impl;
using OneIdentity.SafeguardDotNet;


namespace OneIdentity.SafeguardDevOpsService.ConfigurationImpl
{
    internal class ConfigurationLogic : IConfigurationLogic
    {
        private int _safeguardApiVersion = 3;
        private bool _safeguardIgnoreSsl = true;

        private readonly IConfigurationRepository _configurationRepository;

        public ConfigurationLogic(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository;
        }


        public Configuration InitialConfiguration(InitialConfiguration initialConfig)
        {
            //TODO: Create a new configuration element here
            //TODO: Check to see if there is already a configuration.  If so, throw.
            //TODO: Get the registration and store the configuration in the database

            if (initialConfig == null)
                throw new Exception("The initial configuration cannot be null.");
            if (initialConfig.CertificateUserThumbprint == null)
                throw new Exception("The user certificate thumbprint cannot be null.");
            if (initialConfig.SpsAddress == null)
                throw new Exception("The SPS network address cannot be null.");

            ISafeguardConnection connection = null;
            try
            {

                connection = Safeguard.Connect(initialConfig.SpsAddress, initialConfig.CertificateUserThumbprint,
                    _safeguardApiVersion, _safeguardIgnoreSsl);

                var rawJson = connection.InvokeMethod(Service.Core, Method.Get, "A2ARegistrations");

                var registrations = JsonHelper.DeserializeObject<IEnumerable<SppRegistration>>(rawJson);

                // TODO: Assume that we only have one registration that belongs to the cert user
                var registration = registrations?.FirstOrDefault();
                if (registration != null)
                {
                    var configuration = new Configuration
                    {
                        SpsAddress = initialConfig.SpsAddress,
                        A2ARegistrationId = registration.Id,
                        A2ARegistrationName = registration.AppName,
                        CertificateUser = registration.CertificateUser,
                        CertificateUserThumbPrint = registration.CertificateUserThumbPrint,
                        CreatedByUserId = registration.CreatedByUserId,
                        CreatedByUserDisplayName = registration.CreatedByUserDisplayName,
                        CreatedDate = registration.CreatedDate,
                        AccountMapping = new List<AccountMapping>()
                    };
//                configuration.AccountMapping = GetAccountMappings(configuration);

                    _configurationRepository.SaveConfiguration(configuration);
                    return configuration;
                }
            }
            finally
            {
                connection?.LogOut();
            }

            throw new Exception("Failed to configure devops.");
        }

        public void DeleteConfiguration()
        {
            _configurationRepository.DeleteConfiguration();
        }

        public Registration GetRegistration()
        {
            return _configurationRepository.GetConfiguration();
        }

        public Configuration UpdateConnectionConfiguration(ConnectionConfiguration connectionConfig)
        {
            if (connectionConfig == null)
                throw new Exception("The initial configuration cannot be null.");
            if (connectionConfig.CertificateUserThumbprint == null)
                throw new Exception("The user certificate thumbprint cannot be null.");
            if (connectionConfig.SpsAddress == null)
                throw new Exception("The SPS network address cannot be null.");

            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null) return null;

            configuration.CertificateUserThumbPrint = connectionConfig.CertificateUserThumbprint;
            configuration.SpsAddress = connectionConfig.SpsAddress;

            //Validate the connection information
            var connection = Safeguard.Connect(connectionConfig.SpsAddress,
                connectionConfig.CertificateUserThumbprint, _safeguardApiVersion, _safeguardIgnoreSsl);
            connection?.LogOut();

            _configurationRepository.SaveConfiguration(configuration);

            return configuration;
        }

        public IEnumerable<AccountMapping> GetAccountMappings(string accountName = "", string vaultName = "")
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null) return null;


            if (String.IsNullOrEmpty(accountName) && String.IsNullOrEmpty(vaultName))
                return configuration.AccountMapping.ToArray();

            var accountMappings = configuration.AccountMapping.Where(x =>
                x.AccountName.StartsWith(accountName) || x.VaultName.StartsWith(vaultName));
            return accountMappings;
        }

        public IEnumerable<AccountMapping> SaveAccountMappings(IEnumerable<AccountMapping> newAccountMappings)
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null) return null;

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
            if (configuration == null) return null;

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

            var configJson = JsonHelper.SerializeObject<Configuration>(configuration);
            _configurationRepository.SaveConfiguration(configuration);

            return configuration.AccountMapping;
        }

        public IEnumerable<RetrievableAccount> GetRetrievableAccounts()
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null) return null;

            ISafeguardConnection connection = null;
            try
            {
                connection = Safeguard.Connect(configuration.SpsAddress, configuration.CertificateUserThumbPrint,
                    _safeguardApiVersion, _safeguardIgnoreSsl);
                var rawJson = connection.InvokeMethod(Service.Core, Method.Get,
                    $"A2ARegistrations/{configuration.A2ARegistrationId}/RetrievableAccounts");
                var retrievableAccounts = JsonHelper.DeserializeObject<IEnumerable<RetrievableAccount>>(rawJson);

                return retrievableAccounts.ToList();
            }
            finally
            {
                connection?.LogOut();
            }
        }

        private IEnumerable<AccountMapping> GetAccountMappings(Configuration configuration)
        {
            ISafeguardConnection connection = null;
            try
            {
                connection = Safeguard.Connect(configuration.SpsAddress, configuration.CertificateUserThumbPrint,
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
                connection?.LogOut();
            }
        }

        private RetrievableAccount GetRetrievableAccount(Configuration configuration, string apiKey)
        {
            var apiKeyInfo = _configurationRepository.GetSetting(apiKey);

            ISafeguardConnection connection = null;
            try
            {

                connection = Safeguard.Connect(configuration.SpsAddress, configuration.CertificateUserThumbPrint,
                    _safeguardApiVersion, _safeguardIgnoreSsl);
                var rawJson = connection.InvokeMethod(Service.Core, Method.Get,
                    $"A2ARegistrations/{configuration.A2ARegistrationId}/RetrievableAccounts/{apiKeyInfo.Value}");

                var retrievableAccount = JsonHelper.DeserializeObject<IEnumerable<RetrievableAccount>>(rawJson);

                return retrievableAccount?.FirstOrDefault();
            }
            finally
            {
                connection?.LogOut();
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

    }
}
