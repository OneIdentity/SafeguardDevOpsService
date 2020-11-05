using System;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.KubernetesSecrets
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static Kubernetes _client;
        private static Dictionary<string,string> _configuration;
        private static ILogger _logger;

        private const string ConfigFilePathName = "configFilePath";
        private const string VaultNamespaceName = "vaultNamespace";

        private static string _defaultNamespace = "default";

        public string Name => "KubernetesSecrets";
        public string DisplayName => "Kubernetes Secrets";
        public string Description => "This is the Kubernetes Secrets plugin for updating passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { ConfigFilePathName, "" },
                { VaultNamespaceName, _defaultNamespace }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null)
            {
                _configuration = configuration;
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
                _logger.Error("Some parameters are missing from the configuration or the configuration file is invalid.");
            }
        }

        public bool SetPassword(string asset, string account, string password)
        {
            if (_client == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var vaultNamespace = _defaultNamespace;
            if (_configuration != null && _configuration.ContainsKey(VaultNamespaceName))
            {
                vaultNamespace = _configuration[VaultNamespaceName];
            }

            var passwordData = new Dictionary<string, string> {{"password", password}};
            var data = new Dictionary<string, byte[]>();

            V1Secret secret = null;
            try
            {
                secret = _client.ReadNamespacedSecret($"{asset}-{account}", vaultNamespace);
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
                            Name = $"{asset}-{account}",
                            NamespaceProperty = vaultNamespace
                        }
                    };
                    _client.CreateNamespacedSecret(secret, vaultNamespace);
                }
                else
                {
                    secret.StringData = passwordData;
                    _client.ReplaceNamespacedSecret(secret, $"{asset}-{account}", vaultNamespace);
                }

                _logger.Information($"Password for {asset}-{account} has been successfully stored in the vault.");
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
