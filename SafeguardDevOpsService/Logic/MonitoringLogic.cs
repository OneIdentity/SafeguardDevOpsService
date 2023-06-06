
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
        private static FixedSizeQueue<MonitorEvent> _lastEventsQueue = new FixedSizeQueue<MonitorEvent>(10000);

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
                _logger.Error(ex, $"Could not restore the last known running state of the monitor. {ex.Message}");
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

            if (ignoreSsl.Value)
                throw new DevOpsException("Monitoring cannot be enabled until a secure connection has been established. Trusted certificates may be missing.");

            // This call will fail if the monitor is being started as part of the service start up.
            //  The reason why is because at service startup, the user has not logged into Secrets Broker yet
            //  so Secrets Broker does not have the SPP credentials that are required to query the current vault account credentials.
            //  However, the monitor can still be started using the existing vault credentials. If syncing doesn't appear to be working
            //  the monitor can be stopped and restarted which will cause a refresh of the vault credentials.
            _pluginManager.RefreshPluginCredentials();

            // connect to Safeguard
            _a2AContext = Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, CertificateValidationCallback, apiVersion.Value);
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

            InitialPasswordPull();

            _logger.Information("Password change monitoring has been started.");
        }

        private void StopMonitoring()
        {
            try
            {
                try
                {
                    _eventListener?.Stop();
                } catch { }

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

                var selectedAccounts = _configDb.GetAccountMappings().Where(a => a.ApiKey.Equals(apiKey)).ToList();

                // At this point we should have one API key to retrieve.
                PullAndPushPasswordByApiKey(apiKey, selectedAccounts);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Password change handler failed: {ex.Message}.");
            }
        }

        private void InitialPasswordPull()
        {
            try
            {
                var apiKeys = _retrievableAccounts.GroupBy(x => x.ApiKey).Select(x => x.First().ApiKey).ToArray();

                // Make sure that we have at least one plugin mapped to the account
                if (!apiKeys.Any())
                    return;

                var accounts = _configDb.GetAccountMappings().ToList();
                foreach (var apiKey in apiKeys)
                {
                    var selectedAccounts = accounts.Where(a => a.ApiKey.Equals(apiKey));
                    PullAndPushPasswordByApiKey(apiKey, selectedAccounts);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Password change handler failed: {ex.Message}.");
            }
        }

        private void PullAndPushPasswordByApiKey(string a2AApiKey, IEnumerable<AccountMapping> selectedAccounts)
        {
            var credentialCache = new Dictionary<CredentialType, string[]>();

            foreach (var account in selectedAccounts)
            {
                var pluginInfo = _configDb.GetPluginByName(account.VaultName);
                var credentialType = Enum.GetName(typeof(CredentialType), pluginInfo.AssignedCredentialType);

                var monitorEvent = new MonitorEvent()
                {
                    Event = $"Sending {credentialType} for account {account.AccountName} to {account.VaultName}.",
                    Result = WellKnownData.SentPasswordSuccess,
                    Date = DateTime.UtcNow
                };

                if (!_pluginManager.IsLoadedPlugin(account.VaultName) || pluginInfo.IsDisabled)
                {
                    monitorEvent.Event = $"{account.VaultName} is disabled or not loaded. No {credentialType} sent for account {account.AccountName}.";
                    monitorEvent.Result = WellKnownData.SentPasswordFailure;
                }
                else
                {
                    if (!credentialCache.ContainsKey(pluginInfo.AssignedCredentialType))
                    {
                        var credential = _pluginManager.GetAccountCredential(pluginInfo.Name, a2AApiKey, pluginInfo.AssignedCredentialType);
                        if (credential is { Length: <= 0 })
                        {
                            monitorEvent.Event = $"Failed to get the {credentialType} from Safeguard for plugin {account.VaultName}. No {credentialType} sent for account {account.AccountName}.";
                            monitorEvent.Result = WellKnownData.SentPasswordFailure;
                            _lastEventsQueue.Enqueue(monitorEvent);
                            _logger.Error(monitorEvent.Event);
                            continue;
                        }

                        credentialCache.Add(pluginInfo.AssignedCredentialType, credential);
                    }

                    try
                    {
                        _logger.Information(monitorEvent.Event);
                        if (!_pluginManager.SendCredential(account.VaultName, 
                                account.AssetName, account.AccountName, credentialCache[pluginInfo.AssignedCredentialType], pluginInfo.AssignedCredentialType, 
                                string.IsNullOrEmpty(account.AltAccountName) ? null : account.AltAccountName))
                        {
                            monitorEvent.Event = $"Unable to set the {credentialType} for {account.AccountName} to {account.VaultName}.";
                            monitorEvent.Result = WellKnownData.SentPasswordFailure;
                            _logger.Error(monitorEvent.Event);
                        }
                    }
                    catch (Exception ex)
                    {
                        monitorEvent.Event = $"Unable to set the {credentialType} for {account.AccountName} to {account.VaultName}: {ex.Message}.";
                        monitorEvent.Result = WellKnownData.SentPasswordFailure;
                        _logger.Error(ex, monitorEvent.Event);
                    }
                }

                _lastEventsQueue.Enqueue(monitorEvent);
            }
        }

    }
}
