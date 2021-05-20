
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using OneIdentity.DevOps.Common;
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
        private static FixedSizeQueue<MonitorEvent> _lastEventsQueue = new FixedSizeQueue<MonitorEvent>(1000);

        public MonitoringLogic(IConfigurationRepository configDb, IPluginManager pluginManager)
        {
            _configDb = configDb;
            _pluginManager = pluginManager;
            _logger = Serilog.Log.Logger;
        }

        bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return CertificateHelper.CertificateValidation(sender, certificate, chain, sslPolicyErrors, _logger, _configDb);
        }

        public void EnableMonitoring(bool enable)
        {
            if (enable)
                StartMonitoring();
            else
                StopMonitoring();

            _configDb.LastKnownMonitorState = GetMonitorState().Enabled ? WellKnownData.MonitorEnabled : WellKnownData.MonitorDisabled;
        }

        public MonitorState GetMonitorState()
        {
            return new MonitorState()
            {
                Enabled = _eventListener != null && _a2AContext != null
            };
        }

        public IEnumerable<MonitorEvent> GetMonitorEvents(int size)
        {
            if (size <= 0)
                size = 25;
            if (size > _lastEventsQueue.Count)
                size = _lastEventsQueue.Count;
            return _lastEventsQueue.TakeLast(size).Reverse();
        }

        public void Run()
        {
            try
            {
                if (_configDb.LastKnownMonitorState != null &&
                    _configDb.LastKnownMonitorState.Equals(WellKnownData.MonitorEnabled))
                {
                    StartMonitoring();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Could not restore the last known running state of the monitor. {ex.Message}");
            }
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
                _logger.Error("No safeguardConnection was found.  Safeguard Secrets Broker for DevOps must be configured first");
                return;
            }

            _pluginManager.RefreshPluginCredentials();

            // connect to Safeguard
            _a2AContext = (ignoreSsl == true) ? Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, apiVersion.Value, true) : 
                Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, CertificateValidationCallback, apiVersion.Value);
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
                var apiKeys = _retrievableAccounts.Where(mp => mp.AssetName == eventInfo.AssetName && mp.AccountName == eventInfo.AccountName).ToArray();

                // Make sure that we have at least one plugin mapped to the account
                if (!apiKeys.Any())
                    _logger.Error("No API keys were found by the password change handler.");

                // Make sure that if there are more than one mapped plugin, all of the API key match for the same account
                var apiKey = apiKeys.FirstOrDefault()?.ApiKey;
                if (!apiKeys.All(x => x.ApiKey.Equals(apiKey)))
                    _logger.Error("Mismatched API keys for the same account were found by the password change handler.");

                // At this point we should have one API key to retrieve.
                using (var password = _a2AContext.RetrievePassword(apiKey.ToSecureString()))
                {
                    var accounts = _configDb.GetAccountMappings().ToList();
                    var selectedAccounts = accounts.Where(a => a.ApiKey.Equals(apiKey));
                    foreach (var account in selectedAccounts)
                    {
                        var monitorEvent = new MonitorEvent()
                        {
                            Event = $"Sending password for account {account.AccountName} to {account.VaultName}.",
                            Result = WellKnownData.SentPasswordSuccess,
                            Date = DateTime.UtcNow
                        };

                        if (_pluginManager.IsDisabledPlugin(account.VaultName))
                        {
                            monitorEvent.Event = $"{account.VaultName} is disabled or not loaded. No password sent for account {account.AccountName}.";
                            monitorEvent.Event = WellKnownData.SentPasswordFailure;
                        }
                        else
                        {
                            try
                            {
                                _logger.Information(monitorEvent.Event);
                                if (!_pluginManager.SendPassword(account.VaultName, account.AssetName,
                                    account.AccountName, password))
                                {
                                    _logger.Error(
                                        $"Unable to set the password for {account.AccountName} to {account.VaultName}.");
                                    monitorEvent.Result = WellKnownData.SentPasswordFailure;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(
                                    $"Unable to set the password for {account.AccountName} to {account.VaultName}: {ex.Message}.");
                                monitorEvent.Result = WellKnownData.SentPasswordFailure;
                            }
                        }

                        _lastEventsQueue.Enqueue(monitorEvent);
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
