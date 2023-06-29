
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
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
        private readonly ICredentialManager _credentialManager;
        private readonly ISafeguardLogic _safeguardLogic;

        private static ISafeguardEventListener _eventListener;
        private static ISafeguardA2AContext _a2AContext;
        private static List<AccountMapping> _retrievableAccounts;
        private static FixedSizeQueue<MonitorEvent> _lastEventsQueue = new FixedSizeQueue<MonitorEvent>(10000);
        private static bool _reverseFlowEnabled = false;

        public MonitoringLogic(IConfigurationRepository configDb, IPluginManager pluginManager, 
            ICredentialManager credentialManager, ISafeguardLogic safeguardLogic)
        {
            _configDb = configDb;
            _pluginManager = pluginManager;
            _logger = Serilog.Log.Logger;
            _credentialManager = credentialManager;
            _safeguardLogic = safeguardLogic;
        }

        bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return CertificateHelper.CertificateValidation(sender, certificate, chain, sslPolicyErrors, _logger, _configDb);
        }

        private bool _isA2AMonitoringEnabled => _eventListener != null && _a2AContext != null;

        public void EnableMonitoring(bool enable)
        {
            // Force the enable state for A2A and reverseflow monitoring.
            var newState = new FullMonitorState()
            {
                Enabled = enable,
                ReverseFlowMonitorState = null
            };

            EnableMonitoring(newState);
        }

        public void EnableMonitoring(FullMonitorState monitorState)
        {
            // If no reverse flow monitor state was provided, assume it needs to follow
            //  the A2A monitor state.
            if (monitorState.ReverseFlowMonitorState == null)
            {
                monitorState.ReverseFlowMonitorState = new ReverseFlowMonitorState()
                {
                    Enabled = monitorState.Enabled,
                    ReverseFlowPollingInterval = _configDb.ReverseFlowPollingInterval ?? WellKnownData.ReverseFlowMonitorPollingInterval
                };
            }

            // If the A2A monitor should start and is stopped, start it.
            if (monitorState.Enabled && !_isA2AMonitoringEnabled)
            {
                StartMonitoring();
            }

            // If the reverse flow monitor should start and is stopped, start it.
            if (monitorState.ReverseFlowMonitorState.Enabled && !_reverseFlowEnabled)
            {
                StartReverseFlowMonitor();
            }

            // If the A2A monitor should stop and it is running, stop it.
            if (!monitorState.Enabled && _isA2AMonitoringEnabled)
            {
                StopMonitoring();
            }

            // If the reverse flow monitor should stopt and is running, stop it.
            if (!monitorState.ReverseFlowMonitorState.Enabled && _reverseFlowEnabled)
            {
                StopReverseFlowMonitor();
            }

            var state = GetFullMonitorState();
            _configDb.LastKnownMonitorState = state.Enabled ? WellKnownData.MonitorEnabled : WellKnownData.MonitorDisabled;
            _configDb.LastKnownReverseFlowMonitorState = state.ReverseFlowMonitorState.Enabled ? WellKnownData.MonitorEnabled : WellKnownData.MonitorDisabled;
        }

        public MonitorState GetMonitorState()
        {
            return new MonitorState()
            {
                Enabled = _isA2AMonitoringEnabled,
            };
        }

        public FullMonitorState GetFullMonitorState()
        {
            return new FullMonitorState()
            {
                Enabled = _isA2AMonitoringEnabled,
                ReverseFlowMonitorState = GetReverseFlowMonitorState()
            };
        }

        public ReverseFlowMonitorState GetReverseFlowMonitorState()
        {
            return new ReverseFlowMonitorState()
            {
                Enabled = _reverseFlowEnabled,
                ReverseFlowPollingInterval = _configDb.ReverseFlowPollingInterval ?? WellKnownData.ReverseFlowMonitorPollingInterval
            };
        }

        public ReverseFlowMonitorState SetReverseFlowMonitorState(ReverseFlowMonitorState reverseFlowMonitorState)
        {
            if (reverseFlowMonitorState == null)
            {
                var msg = "The reverse flow monitor cannot be null.";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }

            if (_configDb.ReverseFlowPollingInterval != reverseFlowMonitorState.ReverseFlowPollingInterval)
            {
                _configDb.ReverseFlowPollingInterval = reverseFlowMonitorState.ReverseFlowPollingInterval;
            }

            // If the reverse flow monitor should start and is not running, start it.
            if (reverseFlowMonitorState.Enabled && !_reverseFlowEnabled)
            {
                StartReverseFlowMonitor();
            }
            // If the reverse flow monitor should stop and is running, stop it.
            if (!reverseFlowMonitorState.Enabled && _reverseFlowEnabled)
            {
                StopReverseFlowMonitor();
            }

            _configDb.LastKnownReverseFlowMonitorState = reverseFlowMonitorState.Enabled
                ? WellKnownData.MonitorEnabled
                : WellKnownData.MonitorDisabled;

            return GetReverseFlowMonitorState();
        }

        public IEnumerable<MonitorEvent> GetMonitorEvents(int size)
        {
            if (size <= 0)
                size = 25;
            if (size > _lastEventsQueue.Count)
                size = _lastEventsQueue.Count;
            return _lastEventsQueue.TakeLast(size).Reverse();
        }

        public bool PollReverseFlow()
        {
            if (ReverseFlowMonitoringAvailable())
            {

                // If monitoring is running then we can assume that the plugins have
                // proper vault credentials.  If not then we need to refresh the
                // vault credentials.
                if (!GetMonitorState().Enabled)
                {
                    _pluginManager.RefreshPluginCredentials();
                }

                var a2AContext = _a2AContext ?? ConnectA2AContext();
                if (a2AContext == null)
                {
                    var msg = "Failed to connect to Safeguard A2A service. Monitoring cannot be started.";
                    _logger.Error(msg);
                    throw new DevOpsException(msg);
                }

                Task.Run(() => PollReverseFlowInternal(a2AContext));
                return true;
            }

            _logger.Information("Reverse flow monitoring is not available. Check 'Allow Setting Credentials' flag in the A2A registration. ");

            return false;
        }

        public void Run()
        {
            try
            {
                // If the A2A monitoring was enabled before the restart, then monitoring on startup.
                if (_configDb.LastKnownMonitorState != null &&
                    _configDb.LastKnownMonitorState.Equals(WellKnownData.MonitorEnabled))
                {
                    StartMonitoring();
                }
                else
                {
                    // Make sure that the last state of the reverse flow is set to disabled if no monitoring started.
                    _configDb.LastKnownReverseFlowMonitorState = WellKnownData.MonitorDisabled;
                }

                // If reverse flow monitoring was enabled before the restart, then start monitoring on startup.
                if (_configDb.LastKnownReverseFlowMonitorState != null &&
                    _configDb.LastKnownReverseFlowMonitorState.Equals(WellKnownData.MonitorEnabled))
                {
                    StartReverseFlowMonitor();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Could not restore the last known running state of the monitor. {ex.Message}");
            }
        }

        private ISafeguardA2AContext ConnectA2AContext()
        {
            var sppAddress = _configDb.SafeguardAddress;
            var userCertificate = _configDb.UserCertificateBase64Data;
            var passPhrase = _configDb.UserCertificatePassphrase?.ToSecureString();
            var apiVersion = _configDb.ApiVersion;
            var ignoreSsl = _configDb.IgnoreSsl;

            if (sppAddress == null || userCertificate == null || !apiVersion.HasValue || !ignoreSsl.HasValue)
            {
                _logger.Error("No safeguardConnection was found.  Safeguard Secrets Broker for DevOps must be configured first");
                return null;
            }

            // connect to Safeguard
            return Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, CertificateValidationCallback, apiVersion.Value);
        }

        private void StartMonitoring()
        {
            if (!_isA2AMonitoringEnabled)
            {
                var ignoreSsl = _configDb.IgnoreSsl;
                if (ignoreSsl.HasValue && ignoreSsl.Value)
                    throw new DevOpsException(
                        "Monitoring cannot be enabled until a secure connection has been established. Trusted certificates may be missing.");

                // connect to Safeguard
                _a2AContext = ConnectA2AContext();
                if (_a2AContext == null)
                    throw new DevOpsException(
                        "Failed to connect to Safeguard A2A service. Monitoring cannot be started.");

                // This call will fail if the monitor is being started as part of the service start up.
                //  The reason why is because at service startup, the user has not logged into Secrets Broker yet
                //  so Secrets Broker does not have the SPP credentials that are required to query the current vault account credentials.
                //  However, the monitor can still be started using the existing vault credentials. If syncing doesn't appear to be working
                //  the monitor can be stopped and restarted which will cause a refresh of the vault credentials.
                _pluginManager.RefreshPluginCredentials();

                // Make sure that the credentialManager cache is empty.
                _credentialManager.Clear();

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
            else
            {
                _logger.Information("Listener is already running.");
            }
        }

        private void StopMonitoring()
        {
            try
            {
                try
                {
                    _eventListener?.Stop();
                    _logger.Information("Password change monitoring has been stopped.");
                }
                catch
                {
                }

                if (!_reverseFlowEnabled)
                {
                    _a2AContext?.Dispose();
                    _a2AContext = null;
                }
            }
            finally
            {
                _eventListener = null;
                _retrievableAccounts = null;
                _credentialManager.Clear();
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
                        if (credential == null || credential.Length <= 0)
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
                        if (pluginInfo.SupportsReverseFlow && pluginInfo.ReverseFlowEnabled)
                        {
                            // Only store passwords and ssh keys in the credential manager for reverse flow comparison. API keys are not supported yet.
                            if (pluginInfo.AssignedCredentialType != CredentialType.ApiKey)
                            {
                                _credentialManager.Upsert(credentialCache[pluginInfo.AssignedCredentialType][0], account, pluginInfo.AssignedCredentialType);
                            }
                        }
                        else
                        {
                            _logger.Information(monitorEvent.Event);
                            if (!_pluginManager.SendCredential(account, credentialCache[pluginInfo.AssignedCredentialType], pluginInfo.AssignedCredentialType))
                            {
                                monitorEvent.Event = $"Unable to set the {credentialType} for {account.AccountName} to {account.VaultName}.";
                                monitorEvent.Result = WellKnownData.SentPasswordFailure;
                                _logger.Error(monitorEvent.Event);
                            }
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

        private CancellationTokenSource _cts = null;

        private void StartReverseFlowMonitor()
        {
            if (!_reverseFlowEnabled && ReverseFlowMonitoringAvailable())
            {
                if (_cts == null)
                {
                    _cts = new CancellationTokenSource();

                    Task.Run(() => ReverseFlowMonitorThread(_cts.Token), _cts.Token);
                }
                else
                {
                    _logger.Information("Reverse monitor thread shutting down.");
                }
            }
        }

        private void StopReverseFlowMonitor()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts = null;
            }
        }

        private bool ReverseFlowMonitoringAvailable()
        {
            using var sg = _safeguardLogic.Connect();

            try
            {
                var result = sg.InvokeMethodFull(Service.Core, Method.Get, $"A2ARegistrations/{_configDb.A2aRegistrationId}");
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var registration =  JsonHelper.DeserializeObject<A2ARegistration>(result.Body);
                    if (registration != null && registration.BidirectionalEnabled.HasValue && registration.BidirectionalEnabled.Value)
                    {
                        return true;
                    }
                }
            }
            catch (SafeguardDotNetException ex)
            {
                if (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    _logger.Error(ex, $"Registration not found for id '{_configDb.A2aRegistrationId}'");
                }
                else
                {
                    var msg = $"Failed to get the registration for id '{_configDb.A2aRegistrationId}'";
                    _logger.Error(ex, msg);
                }
            }
            catch (Exception ex)
            {
                var msg = $"Failed to get the registration for id '{_configDb.A2aRegistrationId}'";
                _logger.Error(ex, msg);
            }

            return false;
        }

        private async Task ReverseFlowMonitorThread(CancellationToken token)
        {
            try
            {
                _reverseFlowEnabled = true;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var delayTime = _configDb.ReverseFlowPollingInterval ?? WellKnownData.ReverseFlowMonitorPollingInterval;
                        await Task.Delay(TimeSpan.FromSeconds(delayTime), token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Information("Reverse flow monitor thread shutting down.");
                    }

                    if (token.IsCancellationRequested || !GetFullMonitorState().ReverseFlowMonitorState.Enabled)
                        break;

                    PollReverseFlowInternal(_a2AContext);
                }
            }
            finally
            {
                _reverseFlowEnabled = false;
                _cts = null;
                if (!_isA2AMonitoringEnabled)
                {
                    _a2AContext?.Dispose();
                    _a2AContext = null;
                }
            }
        }

        private static object _lockReverseFlow = new object();

        private void PollReverseFlowInternal(ISafeguardA2AContext a2AContext)
        {
            lock (_lockReverseFlow)
            {
                var reverseFlowInstances = _configDb.GetAllReverseFlowPluginInstances().ToList();
                foreach (var pluginInstance in reverseFlowInstances)
                {
                    if (_pluginManager.IsLoadedPlugin(pluginInstance.Name) && !pluginInstance.IsDisabled)
                    {
                        var accounts = _configDb.GetAccountMappings(pluginInstance.Name);
                        foreach (var account in accounts)
                        {
                            var monitorEvent = new MonitorEvent()
                            {
                                Event =
                                    $"Getting {pluginInstance.AssignedCredentialType} for account {account.AccountName} to {account.VaultName}.",
                                Result = WellKnownData.GetPasswordSuccess,
                                Date = DateTime.UtcNow
                            };

                            try
                            {
                                _logger.Information(monitorEvent.Event);
                                var fetchedCredential = _pluginManager.GetCredential(account, pluginInstance.AssignedCredentialType);

                                if (fetchedCredential == null)
                                {
                                    monitorEvent.Event =
                                        $"Unable to get the {pluginInstance.AssignedCredentialType} for {account.AccountName} to {account.VaultName}.";
                                    monitorEvent.Result = WellKnownData.GetPasswordFailure;
                                    _logger.Error(monitorEvent.Event);
                                    _lastEventsQueue.Enqueue(monitorEvent);
                                    continue;
                                }

                                if (!_credentialManager.Matches(fetchedCredential, account, pluginInstance.AssignedCredentialType))
                                {
                                    // Push the credential back to SPP here.
                                    a2AContext.SetPassword(account.ApiKey.ToSecureString(), fetchedCredential.ToSecureString());
                                    _credentialManager.Upsert(fetchedCredential, account, pluginInstance.AssignedCredentialType);
                                }

                            }
                            catch (Exception ex)
                            {
                                monitorEvent.Event =
                                    $"Unable to get the {pluginInstance.AssignedCredentialType} for {account.AccountName} to {account.VaultName}: {ex.Message}.";
                                monitorEvent.Result = WellKnownData.GetPasswordFailure;
                                _logger.Error(ex, monitorEvent.Event);
                            }

                            _lastEventsQueue.Enqueue(monitorEvent);
                        }
                    }
                }
            }
        }
    }
}
