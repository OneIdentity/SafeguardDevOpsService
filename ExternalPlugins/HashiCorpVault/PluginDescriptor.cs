using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.HashiCorpVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private VaultConnection _vaultClient;
        private Dictionary<string, string> _configuration;
        private Regex _rgx;

        private const string Address = "http://127.0.0.1:8200";
        private const string MountPoint = "oneidentity";

        private const string AddressName = "address";
        private const string MountPointName = "mountPoint";
        private const string passwordKey = "pw";
        private const string sshkeyKey = "sshkey";
        private string FormatAccountName(string altAccountName, string asset, string account) => _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");

        public string Name => "HashiCorpVault";
        public string DisplayName => "HashiCorp Vault";
        public string Description => "This is the HashiCorp Vault plugin for updating passwords";
        public bool SupportsReverseFlow => true;

        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

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
            if (_configuration != null && credential != null)
            {
                try
                {
                    _vaultClient = new VaultConnection(_configuration[AddressName], credential, Logger);
                    Logger.Information($"Plugin {Name} successfully authenticated.");
                }
                catch (Exception ex)
                {
                    Logger.Information(ex, $"Invalid configuration for {Name}. Please use the api to set a valid configuration. {ex.Message}");
                }
            }
            else
            {
                Logger.Error("The plugin configuration or credential is missing.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_vaultClient == null)
                return false;

            try
            {
                var response = _vaultClient.InvokeMethodFull(HttpMethod.Get, $"v1/{_configuration[MountPointName]}/config");
                Logger.Information($"Test vault connection for {DisplayName}: Result = {response.Body}");
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
            Logger = null;
            _vaultClient = null;
            _configuration.Clear();
            _configuration = null;
        }

        private string GetPassword(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            return FetchCredential(FormatAccountName(altAccountName, asset, account), passwordKey);
        }

        private string GetSshKey(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            return FetchCredential(FormatAccountName(altAccountName, asset, account), sshkeyKey);
        }

        private string SetPassword(string asset, string account, string[] password, string altAccountName)
        {
            if (!ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            if (_configuration == null || _vaultClient == null)
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            if (password is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var name = FormatAccountName(altAccountName, asset, account);

            return StoreCredential(name, "{\"data\": {\""+ passwordKey + "\":\"" + password[0] + "\"}}") ? password[0] : null;
        }

        private string SetSshKey(string asset, string account, string[] sshKey, string altAccountName)
        {
            if (!ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            if (_configuration == null || _vaultClient == null)
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            if (sshKey is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var name = FormatAccountName(altAccountName, asset, account);

            return StoreCredential(name, "{\"data\": {\"" + sshkeyKey + "\":\""+sshKey[0].ReplaceLineEndings(string.Empty)+"\"}}") ? sshKey[0] : null;
        }

        private string SetApiKey(string asset, string account, string[] apiKeys, string altAccountName)
        {
            if (!ValidationHelper.CanHandleApiKey(this))
            {
                return null;
            }

            if (_configuration == null || _vaultClient == null)
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            if (apiKeys == null || apiKeys.Length == 0)
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var name = FormatAccountName(altAccountName, asset, account);
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
                    Logger.Error($"The ApiKey {name} {apiKey.ClientId} failed to save to the {DisplayName} vault.");
                    retval = false;
                }
            }

            var data = keys.Select(apiKey => "\"" + apiKey.ClientId + "\":\"" + apiKey.ClientSecret + "\"").Aggregate(string.Empty, (current, d) => string.IsNullOrEmpty(current) ? d : current + ", " + d);

            if (!StoreCredential(name, "{\"data\": {"+data+"}}"))
            {
                Logger.Error($"Failed to save the ApiKeys for {name} to the {this.DisplayName} vault.");
                retval = false;
            }

            return retval ? "" : null;
        }

        private bool StoreCredential(string name, string payload)
        {
            try
            {
                var response = _vaultClient.InvokeMethodFull(HttpMethod.Post, $"v1/{_configuration[MountPointName]}/data/{name}", payload);

                if (response is { StatusCode: HttpStatusCode.OK })
                {
                    Logger.Information($"The secret for {name} has been successfully stored in the {DisplayName} vault.");
                    return true;
                }

                Logger.Error($"Failed to set the secret for {name} in plugin {DisplayName}.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set the secret for {name} in plugin {DisplayName}: {ex.Message}.");
            }

            return false;
        }

        private string FetchCredential(string name, string type)
        {
            try
            {
                string secret = null;
                var response = _vaultClient.InvokeMethodFull(HttpMethod.Get, $"v1/{_configuration[MountPointName]}/data/{name}");
                if (response is { StatusCode: HttpStatusCode.OK })
                {
                    dynamic data = JsonConvert.DeserializeObject(response.Body);
                    if (data != null)
                    {
                        secret = data["data"]["data"][type];
                        Logger.Information($"The secret for {name} has been fetched from the {DisplayName} vault.");
                        return secret;
                    }
                }
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
