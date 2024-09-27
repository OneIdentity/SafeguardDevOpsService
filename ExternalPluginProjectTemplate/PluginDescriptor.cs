using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using OneIdentity.DevOps.Common;
using Serilog;

namespace $safeprojectname$
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private Dictionary<string, string> _configuration;

        public string Name => "$safeprojectname$";

        public string DisplayName => "$safeprojectname$";

        public string Description => "This is the $safeprojectname$ plugin for updating passwords.";

        public CredentialType[] SupportedCredentialTypes => new[] { CredentialType.Password };

        public bool SupportsReverseFlow => false;

        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

        private bool _debugLog = false;

        /// <summary>Returns a Dictionary that defines the configuration elements that are required by the plugin.
        /// The configuration of every plugin is defined as key/value pairs.</summary>
        /// <returns></returns>
        public Dictionary<string, string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                // The plugin name isn't really a configuration parameter. This is just here
                // for guidance.
                { nameof(Name), "" },
            };
        }

        /// <summary>This method is called whenever a new configuration is updated by calling
        /// PUT /service/devops/v1/Plugins/{name} API or when the plugin is initially loaded by the Safeguard
        /// Secrets Broker service.</summary>
        /// <param name="configuration"></param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void SetPluginConfiguration(Dictionary<string, string> configuration)
        {
            DebugLog($"{nameof(SetPluginConfiguration)} being called.");

            _configuration = configuration;
        }

        /// <summary>This method is called before the TestVaultConnection() method is called or the Safeguard
        /// Secrets Broker A2A monitor is enabled. The implementation of this method should establish an
        /// authenticated connection with the third-party vault and store the connection in memory to be used
        /// whenever credentials need to be pushed to the vault.</summary>
        /// <param name="credential">The Google Cloud service account credential, in the form of a JSON object,
        /// with an RSA private key used to create self-signed JWT auth tokens.</param>
        public void SetVaultCredential(string credential)
        {
            DebugLog($"{nameof(SetVaultCredential)} being called with: {credential}");
        }

        /// <summary>This method is called whenever the API /service/devops/v1/Plugins/{name}/TestConnection is
        /// called. The implementation of the method should use the authenticated connection that was established
        /// when the SetVaultCredentials() method was called and test the connectivity to the third-party vault.</summary>
        /// <returns></returns>
        public bool TestVaultConnection()
        {
            DebugLog($"{nameof(TestVaultConnection)} being called.");

            return true;
        }

        /// <summary>This method is called immediately after the monitor has been enabled, when the Safeguard Secrets
        /// Broker has been notified that a monitored credential changed and when a new credential needs to be pushed
        /// to the corresponding vault. The implementation of this method should use the established connection to the
        /// vault to push the new credential as the specified account name. </summary>
        /// <param name="credentialType"></param>
        /// <param name="asset"></param>
        /// <param name="account"></param>
        /// <param name="credential"></param>
        /// <param name="altAccountName"></param>
        /// <returns></returns>
        public string SetCredential(CredentialType credentialType, string asset, string account, string[] credential, string altAccountName)
        {
            DebugLog($"{nameof(SetCredential)} being called with {nameof(credentialType)}={credentialType} {nameof(asset)}={asset} {nameof(account)}={account} {nameof(credential)}={string.Join(',', credential)} {nameof(altAccountName)}={altAccountName}");

            return credential[0];
        }

        /// <summary>This method is called immediately after the monitor has been enabled and on a polling
        /// schedule that defaults every 60 seconds. This method is not called if the plugin does not support
        /// the Reverse Flow functionality or the plugin instance does not have the Reverse Flow functionality
        /// enabled.</summary>
        /// <param name="credentialType"></param>
        /// <param name="asset"></param>
        /// <param name="account"></param>
        /// <param name="altAccountName"></param>
        /// <returns></returns>
        public string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName)
        {
            // For reverse flow only.

            DebugLog($"{nameof(GetCredential)} being called with {nameof(credentialType)}={credentialType} {nameof(asset)}={asset} {nameof(account)}={account} {nameof(altAccountName)}={altAccountName}");

            return string.Empty;
        }

        /// <summary>This method is called whenever the Safeguard Secrets Broker service is restarted or shutdown.
        /// The implementation of this method should include anything that needs to be done to the plugin to cleanly
        /// shutdown.</summary>
        public void Unload()
        {
            DebugLog($"{nameof(Unload)} being called.");

            // No resources to clean up or do here.
        }

        private void DebugLog(string msg)
        {
            if (_debugLog)
            {
                Logger.Information(msg);
            }
        }
    }
}
