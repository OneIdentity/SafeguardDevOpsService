using System;
using System.Collections.Generic;
using OneIdentity.Common;
using Serilog;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace OneIdentity.HashiCorp
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static IVaultClient _vaultClient = null;
        private static Dictionary<string,string> _configuration = null;
        private static ILogger _logger = null;

        private string _address = "http://127.0.0.1:8200";
        private string _mountPoint = "oneidentity";

        private readonly string _authTokenName = "authToken";
        private readonly string _addressName = "address";
        private readonly string _mountPointName = "mountPoint";

        public PluginDescriptor()
        {
        }

        public string Name { get; } = "HashiCorpVault";
        public string Description { get; } = "This is the HashiCorp Vault plugin for updating the passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            if (_configuration == null)
            {
                _configuration = new Dictionary<string, string>
                {
                    { _authTokenName, "" },
                    { _addressName, _address },
                    { _mountPointName, _mountPoint }
                };
            }

            return _configuration;
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(_authTokenName) &&
                configuration.ContainsKey(_addressName) && configuration.ContainsKey(_mountPointName))
            {
                var authMethod = new TokenAuthMethodInfo(configuration[_authTokenName]);
                var vaultClientSettings = new VaultClientSettings(configuration[_addressName], authMethod);
                _vaultClient = new VaultClient(vaultClientSettings);
                _configuration = configuration;
                _logger.Information($"Plugin {Name} has been successfully configured.");
            }
            else
            {
                _logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public bool SetPassword(string account, string password)
        {
            if (_vaultClient == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var passwordData = new Dictionary<string, object>();
            passwordData.Add("value", password);

            try
            {
                _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(account, passwordData, null, _configuration[_mountPointName])
                    .Wait();
                _logger.Information($"Password for account {account} has been successfully stored in the vault.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to set the secret for {account}: {ex.Message}.");
                return false;
            }
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }
    }
}
