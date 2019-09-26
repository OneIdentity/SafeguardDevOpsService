
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
            //TODO: Upload the trusted certificate to SPP
            //TODO: Store the certificate and private key in the windows certificate store
            //TODO: Create a new certificate user with the thumb print from the trusted certificate
            //TODO: Create a new A2A registration with well known name and description
            //TODO: Add the account names to the A2A registration
            //TODO: Pull and cache the ApiKeys for the A2A accounts
            //TODO: Get the registration and store the configuration in the database

            if (initialConfig == null)
                throw new Exception("The initial configuration cannot be null.");
            if (initialConfig.CertificateUserThumbprint == null)
                throw new Exception("The user certificate thumbprint cannot be null.");
            if (initialConfig.SpsAddress == null)
                throw new Exception("The SPS network address cannot be null.");

            var connection = Safeguard.Connect(initialConfig.SpsAddress, initialConfig.CertificateUserThumbprint, _safeguardApiVersion, _safeguardIgnoreSsl);
//            var a2aContext = Safeguard.A2A.GetContext(_safeguardAddress, _safeguardClientCertificateThumbprint, _safeguardApiVersion, _safeguardIgnoreSsl);

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

                var configJson = JsonHelper.SerializeObject<Configuration>(configuration);
                _configurationRepository.SetSetting(new Setting(){Name = WellKnownData.ConfigurationName,Value = configJson});
                return configuration;
            }

            throw new Exception("Failed to configure devops.");
        }

        public void DeleteConfiguration()
        {
            _configurationRepository.RemoveSetting(WellKnownData.ConfigurationName);
        }

        public Registration GetRegistration()
        {
            return GetConfiguration();
        }

        public Configuration UpdateConnectionConfiguration(ConnectionConfiguration connectionConfig)
        {
            if (connectionConfig == null)
                throw new Exception("The initial configuration cannot be null.");
            if (connectionConfig.CertificateUserThumbprint == null)
                throw new Exception("The user certificate thumbprint cannot be null.");
            if (connectionConfig.SpsAddress == null)
                throw new Exception("The SPS network address cannot be null.");

            var configuration = GetConfiguration();
            if (configuration == null) return null;

            configuration.CertificateUserThumbPrint = connectionConfig.CertificateUserThumbprint;
            configuration.SpsAddress = connectionConfig.SpsAddress;

            //Validate the connection information
            var connection = Safeguard.Connect(connectionConfig.SpsAddress, connectionConfig.CertificateUserThumbprint, _safeguardApiVersion, _safeguardIgnoreSsl);

            var configJson = JsonHelper.SerializeObject<Configuration>(configuration);
            _configurationRepository.SetSetting(new Setting(){Name = WellKnownData.ConfigurationName,Value = configJson});

            return configuration;
        }

        public IEnumerable<AccountMapping> GetAccountMappings(string accountName = "", string vaultName = "")
        {
            var configuration = GetConfiguration();
            if (configuration == null) return null;


            if (String.IsNullOrEmpty(accountName) && String.IsNullOrEmpty(vaultName))
                return configuration.AccountMapping.ToArray();

            var accountMappings = configuration.AccountMapping.Where(x =>
                x.AccountName.StartsWith(accountName) || x.VaultName.StartsWith(vaultName));
            return accountMappings;
        }

        public IEnumerable<AccountMapping> SaveAccountMappings(IEnumerable<AccountMapping> newAccountMappings)
        {
            var configuration = GetConfiguration();
            if (configuration == null) return null;

            var accountMappingList = configuration.AccountMapping.ToList();
            var newAccountMappingsList = newAccountMappings.ToList();

            accountMappingList.AddRange(newAccountMappingsList.Where(p2 => accountMappingList.All(p1 => !p1.Equals(p2))));
            configuration.AccountMapping = accountMappingList;

            var configJson = JsonHelper.SerializeObject<Configuration>(configuration);
            _configurationRepository.SetSetting(new Setting(){Name = WellKnownData.ConfigurationName,Value = configJson});
            return accountMappingList;
        }

        public IEnumerable<AccountMapping> RemoveAccountMappings(bool removeAll, string accountName, string vaultName)
        {
            var configuration = GetConfiguration();
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
            _configurationRepository.SetSetting(new Setting(){Name = WellKnownData.ConfigurationName,Value = configJson});

            return configuration.AccountMapping;
        }

        private Configuration GetConfiguration()
        {
            var setting = _configurationRepository.GetSetting(WellKnownData.ConfigurationName);
            if (setting == null)
                return null;

            var configuration = JsonHelper.DeserializeObject<Configuration>(setting.Value);
            return configuration;
        }
    }
}
