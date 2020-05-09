
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.SafeguardDotNet;
using OneIdentity.SafeguardDotNet.A2A;
using OneIdentity.SafeguardDotNet.Event;
using Safeguard = OneIdentity.SafeguardDotNet.Safeguard;


namespace OneIdentity.DevOps.Logic
{
    internal class MonitoringLogic : IMonitoringLogic
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private readonly IPluginManager _pluginManager;

        private static ISafeguardEventListener _eventListener;
        private static ISafeguardA2AContext _a2AContext;
        private static List<AccountMapping> _retrievableAccounts;

        public MonitoringLogic(IConfigurationRepository configDb, IPluginManager pluginManager)
        {
            _configDb = configDb;
            _pluginManager = pluginManager;
            _logger = Serilog.Log.Logger;
        }

        public void EnableMonitoring(bool enable)
        {
            if (enable)
                StartMonitoring();
            else
                StopMonitoring();
        }

        public MonitorState GetMonitorState()
        {
            return new MonitorState()
            {
                Enabled = _eventListener != null && _a2AContext != null
            };
        }

        private void StartMonitoring()
        {
            if (_eventListener != null)
                throw new DevOpsException("Listener is already running.");

            var sppAddress = _configDb.SafeguardAddress;
            var userCertificate = _configDb.UserCertificateBase64Data;
            var passPhrase = _configDb.UserCertificatePassphrase?.ToSecureString();
            var apiVersion = _configDb.ApiVersion;
            var ignoreSsl = _configDb.IgnoreSsl;

            if (sppAddress == null || userCertificate == null || !apiVersion.HasValue || !ignoreSsl.HasValue)
            {
                _logger.Error("No safeguardConnection was found.  DevOps service must be configured first");
                return;
            }

            // connect to Safeguard
            _a2AContext = Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, apiVersion.Value, ignoreSsl.Value);

            // figure out what API keys to monitor
            _retrievableAccounts = _configDb.GetAccountMappings().ToList();
            if (_retrievableAccounts.Count == 0)
            {
                var msg = "No accounts have been mapped to plugins.  Nothing to do.";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }

            var apiKeys = new List<SecureString>();
            foreach (var account in _retrievableAccounts)
            {
                apiKeys.Add(account.ApiKey.ToSecureString());
            }

            _eventListener = _a2AContext.GetPersistentA2AEventListener(apiKeys, PasswordChangeHandler);
            _eventListener.Start();

            _logger.Information("Password change monitoring has been started.");
        }

        private void StopMonitoring()
        {
            try
            {
                _eventListener?.Stop();
                _a2AContext?.Dispose();
                _logger.Information("Password change monitoring has been stopped.");
            }
            finally
            {
                _eventListener = null;
                _a2AContext = null;
                _retrievableAccounts = null;
            }
        }

        private void PasswordChangeHandler(string eventName, string eventBody)
        {
            var eventInfo = JsonHelper.DeserializeObject<EventInfo>(eventBody);

            try
            {
                var apiKey = _retrievableAccounts.Single(mp => mp.AssetName == eventInfo.AssetName && mp.AccountName == eventInfo.AccountName).ApiKey;
                using (var password = _a2AContext.RetrievePassword(apiKey.ToSecureString()))
                {
                    var accounts = _configDb.GetAccountMappings().ToList();
                    var selectedAccounts = accounts.Where(a => a.ApiKey.Equals(apiKey));
                    foreach (var account in selectedAccounts)
                    {
                        try
                        {
                            _logger.Information($"Sending password for account {account.AccountName} to {account.VaultName}.");
                            if (!_pluginManager.SendPassword(account.VaultName, account.AccountName, password))
                                _logger.Error(
                                    $"Unable to set the password for {account.AccountName} to {account.VaultName}.");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                $"Unable to set the password for {account.AccountName} to {account.VaultName}: {ex.Message}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Password change handler failed: {ex.Message}.");
            }
        }
    }
}
