using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using OneIdentity.DevOps.Common;
using RestSharp;
using Serilog;

namespace OneIdentity.DevOps.HashiCorpVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private VaultConnection _vaultClient;
        private Dictionary<string, string> _configuration;
        private Regex _rgx;
        private ILogger _logger;

        private const string Address = "http://127.0.0.1:8200";
        private const string MountPoint = "oneidentity";

        private const string AddressName = "address";
        private const string MountPointName = "mountPoint";

        public string Name => "HashiCorpVault";
        public string DisplayName => "HashiCorp Vault";
        public string Description => "This is the HashiCorp Vault plugin for updating passwords";
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { AddressName, Address },
                { MountPointName, MountPoint }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(AddressName) && configuration.ContainsKey(MountPointName))
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
            if (_configuration != null && credential != null)
            {
                try
                {
                    _vaultClient = new VaultConnection(_configuration[AddressName], credential, _logger);
                    _logger.Information($"Plugin {Name} successfully authenticated.");
                }
                catch (Exception ex)
                {
                    _logger.Information(ex, $"Invalid configuration for {Name}. Please use the api to set a valid configuration. {ex.Message}");
                }
            }
            else
            {
                _logger.Error("The plugin configuration or credential is missing.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_vaultClient == null)
                return false;

            try
            {
                var response = _vaultClient.InvokeMethodFull(Method.GET, $"v1/{_configuration[MountPointName]}/config");
                _logger.Information($"Test vault connection for {DisplayName}: Result = {response.Body}");
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
            if (AssignedCredentialType != CredentialType.Password)
            {
                _logger.Error("This plugin instance does not handle the Password credential type.");
                return false;
            }

            if (_configuration == null || _vaultClient == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");

            return StoreCredential(name, "{\"data\": {\"pw\":\""+password+"\"}}");
        }

        public bool SetSshKey(string asset, string account, string sshKey, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.SshKey)
            {
                _logger.Error("This plugin instance does not handle the SshKey credential type.");
                return false;
            }

            if (_configuration == null || _vaultClient == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");

            return StoreCredential(name, "{\"data\": {\"sshkey\":\""+sshKey.ReplaceLineEndings(string.Empty)+"\"}}");
        }

        public bool SetApiKey(string asset, string account, string[] apiKeys, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.ApiKey)
            {
                _logger.Error("This plugin instance does not handle the ApiKey credential type.");
                return false;
            }

            if (_configuration == null || _vaultClient == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");
            var keys = new List<ApiKey>();
            var retval = true;

            foreach (var apiKeyJson in apiKeys)
            {
                var apiKey = JsonHelper.DeserializeObject<ApiKey>(apiKeyJson);
                if (apiKey != null)
                {
                    keys.Add(apiKey);
                }
                else
                {
                    _logger.Error($"The ApiKey {name} {apiKey.ClientId} failed to save to the {this.DisplayName} vault.");
                    retval = false;
                }
            }

            var data = keys.Select(apiKey => "\"" + apiKey.ClientId + "\":\"" + apiKey.ClientSecret + "\"").Aggregate(string.Empty, (current, d) => string.IsNullOrEmpty(current) ? d : current + ", " + d);

            if (!StoreCredential(name, "{\"data\": {"+data+"}}"))
            {
                _logger.Error($"Failed to save the ApiKeys for {name} to the {this.DisplayName} vault.");
                retval = false;
            }

            return retval;
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Unload()
        {
            _logger = null;
            _vaultClient = null;
            _configuration.Clear();
            _configuration = null;
        }

        private bool StoreCredential(string name, string payload)
        {

            try
            {
                var response = _vaultClient.InvokeMethodFull(Method.POST, $"v1/{_configuration[MountPointName]}/data/{name}", payload);

                _logger.Information($"The secret for {name} has been successfully stored in the vault.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to set the secret for {name}: {ex.Message}.");
                return false;
            }
        }
    }
}
