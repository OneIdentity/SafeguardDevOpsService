using System;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using OneIdentity.Common;
using Serilog;

namespace OneIdentity.KubernetesVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static Kubernetes _client = null;
        private static Dictionary<string,string> _configuration = null;
        private static ILogger _logger = null;

        private readonly string _configFilePathName = "configFilePath";
        private readonly string _vaultNamespaceName = "vaultNamespace";

        private static string _defaultNamespace = "default";

        public PluginDescriptor()
        {
        }

        public string Name { get; } = "KubernetesVault";
        public string Description { get; } = "This is the Kubenetes Vault plugin for updating passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            if (_configuration == null)
            {
                _configuration = new Dictionary<string, string> {{_configFilePathName, ""}, {_configFilePathName, _defaultNamespace}};
            }

            return _configuration;
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            KubernetesClientConfiguration config = null;
            if (configuration != null)
            {
                _configuration = configuration;
                config = configuration.ContainsKey(_configFilePathName) 
                    ? KubernetesClientConfiguration.BuildConfigFromConfigFile(configuration[_configFilePathName]) 
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

        public bool SetPassword(string account, string password)
        {
            if (_client == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var vaultNamespace = _defaultNamespace;
            if (_configuration != null && _configuration.ContainsKey(_vaultNamespaceName))
            {
                vaultNamespace = _configuration[_vaultNamespaceName];
            }

            var passwordData = new Dictionary<string, string> {{"password", password}};
            var data = new Dictionary<string, byte[]>();

            V1Secret secret = null;
            try
            {
                secret = _client.ReadNamespacedSecret(account, vaultNamespace);
            } catch {}

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
                            Name = account,
                            NamespaceProperty = vaultNamespace
                        }
                    };
                    _client.CreateNamespacedSecret(secret, vaultNamespace);
                }
                else
                {
                    secret.StringData = passwordData;
                    _client.ReplaceNamespacedSecret(secret, account, vaultNamespace);
                }

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
