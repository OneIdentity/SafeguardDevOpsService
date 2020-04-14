using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.AzureKeyVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static IKeyVaultClient _keyVaultClient;
        private static Dictionary<string,string> _configuration;
        private static ILogger _logger;

        private const string ApplicationIdName = "applicationId";
        private const string ClientSecretName = "clientSecret";
        private const string VaultUriName = "vaultUri";

        public string Name => "AzureKeyVault";
        public string Description => "This is the Azure Key Vault plugin for updating passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ?? (_configuration = new Dictionary<string, string>
            {
                { ApplicationIdName, "" },
                { ClientSecretName, "" },
                { VaultUriName, "" }
            });
        }

        private const string ApplicationId = "fc02cf07-7011-47db-a6cb-0794f9b22bdf";
        private const string ClientSecret = "9jxay4HZ.MY_Hl[RBwny3u=KkderucK1";

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(ApplicationIdName) &&
                configuration.ContainsKey(ClientSecretName) && configuration.ContainsKey(VaultUriName))
            {
                _keyVaultClient = new KeyVaultClient(async (authority, resource, scope) =>
                {
                    var adCredential = new ClientCredential(ApplicationId, ClientSecret);
                    var authenticationContext = new AuthenticationContext(authority, null);
                    return (await authenticationContext.AcquireTokenAsync(resource, adCredential)).AccessToken;
                });
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
            if (_keyVaultClient == null || _configuration == null || !_configuration.ContainsKey(VaultUriName))
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            try
            {
                Task.Run(async () => await _keyVaultClient.SetSecretAsync(_configuration[VaultUriName], account, password));
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
