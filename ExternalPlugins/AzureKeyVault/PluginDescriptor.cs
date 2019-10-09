using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using OneIdentity.Common;
using Serilog;

namespace OneIdentity.AzureKeyVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static IKeyVaultClient _keyVaultClient = null;
        private static Dictionary<string,string> _configuration = null;
        private static ILogger _logger = null;

        private readonly string _applicationIdName = "applicationId";
        private readonly string _clientSecretName = "clientSecret";
        private readonly string _vaultUriName = "vaultUri";

        public PluginDescriptor()
        {
        }

        public string Name { get; } = "AzureKeyVault";
        public string Description { get; } = "This is the Azure Key Vault plugin for updating passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            if (_configuration == null)
            {
                _configuration = new Dictionary<string, string>();
                _configuration.Add(_applicationIdName, "");
                _configuration.Add(_clientSecretName, "");
                _configuration.Add(_vaultUriName, "");
            }

            return _configuration;
        }

        private const string _applicationId = "fc02cf07-7011-47db-a6cb-0794f9b22bdf";
        private const string _clientSecret = "9jxay4HZ.MY_Hl[RBwny3u=KkderucK1";

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(_applicationIdName) &&
                configuration.ContainsKey(_clientSecretName) && configuration.ContainsKey(_vaultUriName))
            {
                _keyVaultClient = new KeyVaultClient(async (authority, resource, scope) =>
                {
                    var adCredential = new ClientCredential(_applicationId, _clientSecret);
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
            if (_keyVaultClient == null || _configuration == null || !_configuration.ContainsKey(_vaultUriName))
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            try
            {
                Task.Run(async () => await _keyVaultClient.SetSecretAsync(_configuration[_vaultUriName], account, password));
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
