using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.KubernetesSecrets
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private Kubernetes _client;
        private Dictionary<string,string> _configuration;
        private Regex _rgx;

        private const string ConfigFilePathName = "configFilePath";
        private const string VaultNamespaceName = "vaultNamespace";
        private const string DefaultNamespace = "default";
        private string FormatAccountName(string altAccountName, string asset, string account) => _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");

        public string Name => "KubernetesSecrets";
        public string DisplayName => "Kubernetes Secrets";
        public string Description => "This is the Kubernetes Secrets plugin for updating passwords";
        public bool SupportsReverseFlow => true;
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password};

        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { ConfigFilePathName, "" },
                { VaultNamespaceName, DefaultNamespace }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null)
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
            KubernetesClientConfiguration config;
            if (_configuration != null)
            {
                config = _configuration.ContainsKey(ConfigFilePathName) 
                    ? KubernetesClientConfiguration.BuildConfigFromConfigFile(_configuration[ConfigFilePathName]) 
                    : KubernetesClientConfiguration.BuildDefaultConfig();
            }
            else
            {
                config = KubernetesClientConfiguration.BuildDefaultConfig();
            }

            if (config != null)
            {
                _client = new Kubernetes(config);
            }
            else
            {
                Logger.Error("Some parameters are missing from the configuration or the configuration file is invalid.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_client == null)
                return false;

            var vaultNamespace = DefaultNamespace;
            if (_configuration != null && _configuration.ContainsKey(VaultNamespaceName))
            {
                vaultNamespace = _configuration[VaultNamespaceName];
            }

            try
            {
                var task = Task.Run(async () => await _client.ListNamespacedSecretAsync(vaultNamespace));
                var result = task.Result;
                Logger.Information($"Test vault connection for {DisplayName}: Result = {result}");
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
                case CredentialType.ApiKey:
                    ValidationHelper.CanReverseFlow(this);
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
                    ValidationHelper.CanHandleSshKey(this);
                    break;
                case CredentialType.ApiKey:
                    ValidationHelper.CanHandleApiKey(this);
                    break;
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

            if (_client == null)
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            var vaultNamespace = DefaultNamespace;
            if (_configuration != null && _configuration.ContainsKey(VaultNamespaceName))
            {
                vaultNamespace = _configuration[VaultNamespaceName];
            }

            var name = FormatAccountName(altAccountName, asset, account);

            try
            {
                var secret = _client.ReadNamespacedSecret(name, vaultNamespace);

                Logger.Information($"The secret for {name} has been fetched from the {DisplayName} vault.");
                return secret.StringData["password"];
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to fetch the secret for {name} in the vault {DisplayName}: {ex.Message}.");
            }

            return null;
        }

        private string SetPassword(string asset, string account, string[] password, string altAccountName)
        {
            if (!ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            if (_client == null)
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            if (password is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var vaultNamespace = DefaultNamespace;
            if (_configuration != null && _configuration.ContainsKey(VaultNamespaceName))
            {
                vaultNamespace = _configuration[VaultNamespaceName];
            }

            var passwordData = new Dictionary<string, string> {{"password", password[0]}};
            var data = new Dictionary<string, byte[]>();
            var name = FormatAccountName(altAccountName, asset, account);

            V1Secret secret = null;
            try
            {
                secret = _client.ReadNamespacedSecret(name, vaultNamespace);
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                if (secret == null)
                {
                    secret = new V1Secret() {
                        ApiVersion = "v1",
                        Kind = "Secret",
                        Type = "Opaque",
                        Data = data,
                        StringData = passwordData,
                        Metadata = new V1ObjectMeta()
                        {
                            Name = name,
                            NamespaceProperty = vaultNamespace
                        }
                    };
                    _client.CreateNamespacedSecret(secret, vaultNamespace);
                }
                else
                {
                    secret.StringData = passwordData;
                    _client.ReplaceNamespacedSecret(secret, $"{asset}-{altAccountName ?? account}", vaultNamespace);
                }

                Logger.Information($"Password for {name} has been successfully stored in the vault.");
                return password[0];
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set the secret for {asset}-{altAccountName ?? account}: {ex.Message}.");
                return null;
            }
        }
    }
}
