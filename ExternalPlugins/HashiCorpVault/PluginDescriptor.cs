﻿using System;
using System.Collections.Generic;
using OneIdentity.DevOps.Common;
using Serilog;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace OneIdentity.DevOps.HashiCorpVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static IVaultClient _vaultClient;
        private static Dictionary<string,string> _configuration;
        private static ILogger _logger;

        private const string Address = "http://127.0.0.1:8200";
        private const string MountPoint = "oneidentity";

        private const string AddressName = "address";
        private const string MountPointName = "mountPoint";

        public string Name => "HashiCorpVault";
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
                    var authMethod = new TokenAuthMethodInfo(credential);
                    var vaultClientSettings = new VaultClientSettings(_configuration[AddressName], authMethod);
                    _vaultClient = new VaultClient(vaultClientSettings);
                    _logger.Information($"Plugin {Name} successfully authenticated.");
                }
                catch (Exception ex)
                {
                    _logger.Information($"Invalid configuration for {Name}. Please use the api to set a valid configuration. {ex.Message}");
                }
            }
            else
            {
                _logger.Error("The plugin configuration or credential is missing.");
            }
        }

        public bool SetPassword(string asset, string account, string password)
        {
            if (_vaultClient == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var passwordData = new Dictionary<string, object>
            {
                { "value", password }
            };

            try
            {
                _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync($"{asset}-{account}", passwordData, null, _configuration[MountPointName])
                    .Wait();
                _logger.Information($"Password for {asset} - {account} has been successfully stored in the vault.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to set the secret for {asset} - {account}: {ex.Message}.");
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
