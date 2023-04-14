using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OneIdentity.DevOps.Common;
using RestSharp;
using Serilog;

namespace OneIdentity.DevOps.HashiCorpVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private VaultConnection _vaultClient;
        private Dictionary<string,string> _configuration;
        private Regex _rgx;
        private ILogger _logger;

        private const string Address = "http://127.0.0.1:8200";
        private const string MountPoint = "oneidentity";

        private const string AddressName = "address";
        private const string MountPointName = "mountPoint";

        public string Name => "HashiCorpVault";
        public string DisplayName => "HashiCorp Vault";
        public string Description => "This is the HashiCorp Vault plugin for updating passwords";

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
            if (_configuration == null || _vaultClient == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");
            try
            {
                var payload = "{\"data\": {\"pw\":\""+password+"\"}}";
                var response = _vaultClient.InvokeMethodFull(Method.POST, $"v1/{_configuration[MountPointName]}/data/{name}", payload);

                _logger.Information($"Password for {name} has been successfully stored in the vault.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to set the secret for {name}: {ex.Message}.");
                return false;
            }
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
    }
}
