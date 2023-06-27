using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Newtonsoft.Json;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.AzureKeyVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private SecretClient _secretsClient;
        private Dictionary<string,string> _configuration;
        private Regex _rgx;

        private const string ApplicationIdName = "applicationId";
        private const string VaultUriName = "vaultUri";
        private const string TenantIdName = "tenantId";
        private string FormatAccountName(string altAccountName, string asset, string account) => _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");

        public string Name => "AzureKeyVault";
        public string DisplayName => "Azure Key Vault";
        public string Description => "This is the Azure Key Vault plugin for updating passwords";
        public bool SupportsReverseFlow => true;
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};

        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

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
                Logger.Information($"Plugin {Name} has been successfully configured.");
                _rgx = new Regex("[^a-zA-Z0-9-]");
            }
            else
            {
                Logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            if (_configuration != null)
            {
                _secretsClient = new SecretClient(new Uri(_configuration[VaultUriName]),
                    new ClientSecretCredential(_configuration[TenantIdName], _configuration[ApplicationIdName], credential));
                Logger.Information($"Plugin {Name} has been successfully authenticated to the Azure vault.");
            }
            else
            {
                Logger.Error("The plugin is missing the configuration.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_secretsClient == null)
                return false;

            try
            {
                var result = _secretsClient.GetDeletedSecrets();
                Logger.Information($"Test vault connection for {DisplayName}: Result = {result != null}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
                return false;
            }
        }

        public string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName)
        {
            switch (credentialType)
            {
                case CredentialType.Password:
                    return GetPassword(asset, account, altAccountName);
                case CredentialType.SshKey:
                    return GetSshKey(asset, account, altAccountName);
                case CredentialType.ApiKey:
                    Logger.Error($"The {DisplayName} plugin instance does not fetch the ApiKey credential type.");
                    break;
                default:
                    Logger.Error($"Invalid credential type requested from the {DisplayName} plugin instance.");
                    break;
            }

            return null;
        }

        public string SetCredential(CredentialType credentialType, string asset, string account, string[] credential, string altAccountName)
        {
            switch (credentialType)
            {
                case CredentialType.Password:
                    return SetPassword(asset, account, credential, altAccountName);
                case CredentialType.SshKey:
                    return SetSshKey(asset, account, credential, altAccountName);
                case CredentialType.ApiKey:
                    return SetApiKey(asset, account, credential, altAccountName);
                default:
                    Logger.Error($"Invalid credential type sent to the {DisplayName} plugin instance.");
                    break;
            }

            return null;
        }

        public void Unload()
        {
        }

        private string GetPassword(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            return FetchCredential(FormatAccountName(altAccountName, asset, account));

        }

        private string GetSshKey(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            return FetchCredential(FormatAccountName(altAccountName, asset, account));
        }

        private string SetPassword(string asset, string account, string[] password, string altAccountName)
        {
            if (!ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            if (_secretsClient == null || _configuration == null || !_configuration.ContainsKey(VaultUriName))
            {
                Logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return null;
            }

            if (password is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var name = FormatAccountName(altAccountName, asset, account);
            return StoreCredential(name, password[0]) ? password[0] : null;
        }

        private string SetSshKey(string asset, string account, string[] sshKey, string altAccountName)
        {
            if (!ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            if (_secretsClient == null || _configuration == null || !_configuration.ContainsKey(VaultUriName))
            {
                Logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return null;
            }

            if (sshKey is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var name = FormatAccountName(altAccountName, asset, account);
            return StoreCredential(name, sshKey[0]) ? sshKey[0] : null;
        }

        private string SetApiKey(string asset, string account, string[] apiKeys, string altAccountName)
        {
            if (!ValidationHelper.CanHandleApiKey(this))
            {
                return null;
            }

            if (_secretsClient == null || _configuration == null || !_configuration.ContainsKey(VaultUriName))
            {
                Logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return null;
            }

            if (apiKeys == null || apiKeys.Length == 0)
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");
            var retval = true;

            foreach (var apiKeyJson in apiKeys)
            {
                var apiKey = JsonHelper.DeserializeObject<ApiKey>(apiKeyJson);
                if (apiKey != null)
                {
                    StoreCredential($"{name}-{apiKey.Name}", $"{apiKey.ClientId}:{apiKey.ClientSecret}");
                }
                else
                {
                    Logger.Error($"The ApiKey {name} {apiKey.ClientId} failed to save to the {DisplayName} vault.");
                    retval = false;
                }
            }

            return retval ? "" : null;
        }

        private bool StoreCredential(string name, string payload)
        {
            try
            {
                Task.Run(async () => await _secretsClient.SetSecretAsync(name, payload));

                Logger.Information($"The secret for {name} has been successfully stored in the vault.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set the secret for {name}: {ex.Message}.");
                return false;
            }
        }

        private string FetchCredential(string name)
        {
            try
            {
                var secret = Task.Run(async () => await _secretsClient.GetSecretAsync(name)).Result;
                Logger.Information($"The secret for {name} has been fetched from the {DisplayName} vault.");

                if (secret != null)
                    return secret.Value.Value;

                Logger.Error($"Failed to fetch the secret for {name} in the vault {DisplayName}.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to fetch the secret for {name} in the vault {DisplayName}: {ex.Message}.");
                return null;
            }

            return null;
        }
    }
}
