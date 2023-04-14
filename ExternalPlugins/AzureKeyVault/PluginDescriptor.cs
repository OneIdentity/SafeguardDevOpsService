using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.AzureKeyVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private SecretClient _secretsClient;
        private Dictionary<string,string> _configuration;
        private Regex _rgx;
        private ILogger _logger;

        private const string ApplicationIdName = "applicationId";
        private const string VaultUriName = "vaultUri";
        private const string TenantIdName = "tenantId";

        public string Name => "AzureKeyVault";
        public string DisplayName => "Azure Key Vault";
        public string Description => "This is the Azure Key Vault plugin for updating passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { ApplicationIdName, "" },
                { VaultUriName, "" },
                { TenantIdName, "" }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            // Make sure that the new configuration key is added to the configuration.
            if (!configuration.ContainsKey(TenantIdName))
            {
                configuration.Add(TenantIdName, "");
            }
            if (configuration != null && configuration.ContainsKey(ApplicationIdName) &&
                configuration.ContainsKey(VaultUriName) && configuration.ContainsKey(TenantIdName))
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
                _secretsClient = new SecretClient(new Uri(_configuration[VaultUriName]),
                    new ClientSecretCredential(_configuration[TenantIdName], _configuration[ApplicationIdName], credential));
                _logger.Information($"Plugin {Name} has been successfully authenticated to the Azure vault.");
            }
            else
            {
                _logger.Error("The plugin is missing the configuration.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_secretsClient == null)
                return false;

            try
            {
                var result = _secretsClient.GetDeletedSecrets();
                _logger.Information($"Test vault connection for {DisplayName}: Result = {result != null}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
                return false;
            }
        }

        public bool SetPassword(string asset, string account, string password, string altAccountName = null)
        {
            if (_secretsClient == null || _configuration == null || !_configuration.ContainsKey(VaultUriName))
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            try
            {
                var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");
                Task.Run(async () => await _secretsClient.SetSecretAsync(name, password));
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to set the secret for {asset}-{altAccountName ?? account}: {ex.Message}.");
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
