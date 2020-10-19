using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        private static Regex _rgx;

        private const string ApplicationIdName = "applicationId";
        private const string VaultUriName = "vaultUri";

        public string Name => "AzureKeyVault";
        public string DisplayName => "Azure Key Vault";
        public string Description => "This is the Azure Key Vault plugin for updating passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { ApplicationIdName, "" },
                { VaultUriName, "" }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(ApplicationIdName) &&
                configuration.ContainsKey(VaultUriName))
            {
                _configuration = configuration;
                _logger.Information($"Plugin {Name} has been successfully configured.");
                _rgx = new Regex("[^a-zA-Z0-9-]");
            }
            else
            {
                _logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            if (_configuration != null)
            {
                _keyVaultClient = new KeyVaultClient(async (authority, resource, scope) =>
                {
                    var adCredential = new ClientCredential(_configuration[ApplicationIdName], credential);
                    var authenticationContext = new AuthenticationContext(authority, null);
                    return (await authenticationContext.AcquireTokenAsync(resource, adCredential)).AccessToken;
                });
                _logger.Information($"Plugin {Name} has been successfully authenticated to the Azure vault.");
            }
            else
            {
                _logger.Error("The plugin is missing the configuration.");
            }
        }

        public bool SetPassword(string asset, string account, string password)
        {
            if (_keyVaultClient == null || _configuration == null || !_configuration.ContainsKey(VaultUriName))
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            try
            {
                var name = _rgx.Replace($"{asset}-{account}", "-");
                Task.Run(async () => await _keyVaultClient.SetSecretAsync(_configuration[VaultUriName], name, password));
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to set the secret for {asset}-{account}: {ex.Message}.");
                return false;
            }
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Unload()
        {
        }
    }
}
