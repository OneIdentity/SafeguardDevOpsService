using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using OneIdentity.SafeguardDotNet.A2A;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;

namespace OneIdentity.DevOps.Logic
{
    internal class BackgroundMaintenanceLogic : IHostedService, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private readonly ISafeguardLogic _safeguardLogic;
        private readonly IPluginsLogic _pluginsLogic;

        public BackgroundMaintenanceLogic(IConfigurationRepository configDb, ISafeguardLogic safeguardLogic, IPluginsLogic pluginsLogic)
        {
            _logger = Serilog.Log.Logger;
            _configDb = configDb;
            _safeguardLogic = safeguardLogic;
            _pluginsLogic = pluginsLogic;
        }

        private ISafeguardConnection GetSgConnection()
        {
            var sppAddress = _configDb.SafeguardAddress;
            var userCertificate = _configDb.UserCertificateBase64Data;
            var passPhrase = _configDb.UserCertificatePassphrase?.ToSecureString();
            var apiVersion = _configDb.ApiVersion ?? WellKnownData.DefaultApiVersion;
            var ignoreSsl = _configDb.IgnoreSsl ?? true;

            if (sppAddress != null && userCertificate != null)
            {
                try
                {
                    _logger.Debug("Connecting to Safeguard: {address}");
                    var connection = Safeguard.Connect(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, apiVersion, ignoreSsl);
                    return connection;
                }
                catch (SafeguardDotNetException ex)
                {
                    _logger.Error(ex, $"Failed to connect to Safeguard at '{sppAddress}': {ex.Message}");
                }
            }

            return null;
        }

        private ISafeguardA2AContext GetA2aContext()
        {
            var sppAddress = _configDb.SafeguardAddress;
            var userCertificate = _configDb.UserCertificateBase64Data;
            var passPhrase = _configDb.UserCertificatePassphrase?.ToSecureString();
            var apiVersion = _configDb.ApiVersion ?? WellKnownData.DefaultApiVersion;
            var ignoreSsl = _configDb.IgnoreSsl ?? true;

            if (sppAddress != null && userCertificate != null)
            {
                try
                {
                    _logger.Debug("Connecting to Safeguard A2A context: {address}");
                    var a2AContext = Safeguard.A2A.GetContext(sppAddress, Convert.FromBase64String(userCertificate), passPhrase, apiVersion, ignoreSsl);
                    return a2AContext;
                }
                catch (SafeguardDotNetException ex)
                {
                    _logger.Error(ex, $"Failed to connect to Safeguard A2A context at '{sppAddress}': {ex.Message}");
                }
            }

            return null;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(async () => await StartAddOnBackgroundMaintenance(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StartAddOnBackgroundMaintenance(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!_safeguardLogic.PauseBackgroudMaintenance)
                {
                    try
                    {
                        using var sgConnection = GetSgConnection();

                        if (sgConnection != null)
                        {
                            CheckAndAddSecretsBrokerInstance(sgConnection);
                            CheckAndPushAddOnCredentials(sgConnection);
                            CheckAndConfigureAddonPlugins(sgConnection);
                            CheckAndSyncVaultCredentials(sgConnection);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"[Background Maintenance] {ex.Message}");
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }

        private void CheckAndConfigureAddonPlugins(ISafeguardConnection sgConnection)
        {
            var addons = _configDb.GetAllAddons();
            foreach (var addon in addons)
            {
                var plugin = _configDb.GetPluginByName(addon.Manifest.PluginName);
                if (plugin != null && plugin.IsSystemOwned != addon.Manifest.IsPluginSystemOwned)
                {
                    plugin.IsSystemOwned = addon.Manifest.IsPluginSystemOwned;
                    _configDb.SavePluginConfiguration(plugin);
                }

                if (!string.IsNullOrEmpty(addon.VaultAccountName) 
                    && addon.VaultAccountId.HasValue 
                    && addon.VaultAccountId > 0
                    && !string.IsNullOrEmpty(addon.Manifest?.PluginName))
                {
                    plugin = _configDb.GetPluginByName(addon.Manifest.PluginName);
                    if (plugin != null && plugin.VaultAccountId != addon.VaultAccountId)
                    {
                        plugin.VaultAccountId = addon.VaultAccountId;
                        _pluginsLogic.SavePluginVaultAccount(sgConnection, plugin.Name, new AssetAccount(){Id = addon.VaultAccountId.Value});
                    }
                }
            }
        }

        private void CheckAndPushAddOnCredentials(ISafeguardConnection sgConnection)
        {
            if (_safeguardLogic.DevOpsSecretsBroker?.Asset == null)
                return;

            var addons = _configDb.GetAllAddons().ToList();
            if (!addons.Any())
                return;
            
            var secretsBrokerAccounts = _safeguardLogic.GetSecretsBrokerAccounts(sgConnection);
            if (secretsBrokerAccounts != null)
            {
                foreach (var addon in addons)
                {
                    // Determine if there are any accounts that need to be pushed to Safeguard.
                    var accounts = new List<DevOpsSecretsBrokerAccount>();
                    foreach (var credential in addon.VaultCredentials)
                    {
                        if (secretsBrokerAccounts.All(x => x.AccountName != credential.Key))
                        {
                            accounts.Add(new DevOpsSecretsBrokerAccount()
                            {
                                AccountId = 0,
                                AccountName = credential.Key,
                                Description = addon.Manifest.DisplayName + " account",
                                AssetId = _safeguardLogic.DevOpsSecretsBroker.Asset.Id,
                                Password = credential.Value
                            });
                        }
                    }

                    // Add any missing accounts to Safeguard through the DevOps/SecretsBroker APIs which will also create an asset to tie them together.
                    if (accounts.Any())
                    {
                        var secretsBrokerAccountsStr = JsonHelper.SerializeObject(accounts);
                        try
                        {
                            var result = SafeguardLogic.DevOpsInvokeMethodFull(_configDb.SvcId, sgConnection, Service.Core, Method.Post,
                                $"DevOps/SecretsBrokers/{_safeguardLogic.DevOpsSecretsBroker.Id}/Accounts/Add",
                                secretsBrokerAccountsStr);
                            if (result.StatusCode == HttpStatusCode.OK)
                            {
                                // Refresh the secrets broker account list after the additions
                                secretsBrokerAccounts = _safeguardLogic.GetSecretsBrokerAccounts(sgConnection);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, 
                                $"Failed to sync the credentials for the Add-On {addon.Name}: {ex.Message}");
                        }
                    }

                    // Make sure that the vault account and asset information has been saved to the AddOn object in the database.
                    if (!string.IsNullOrEmpty(addon.VaultAccountName))
                    {
                        var vaultAccount = secretsBrokerAccounts.FirstOrDefault(x => x.AccountName.StartsWith(addon.VaultAccountName));
                        if (vaultAccount != null && addon.VaultAccountId != vaultAccount.AccountId)
                        {
                            addon.VaultAccountId = vaultAccount.AccountId;
                            addon.VaultAccountName = vaultAccount.AccountName;
                            addon.VaultAssetId = vaultAccount.AssetId;
                            addon.VaultAssetName = vaultAccount.AssetName;
                            _configDb.SaveAddon(addon);
                        }
                    }

                    // Make sure that all vault accounts have been added to the assigned vault A2A registration.
                    if (_configDb.A2aVaultRegistrationId != null && secretsBrokerAccounts.Any())
                    {
                        var a2aAccounts = _safeguardLogic.GetA2ARetrievableAccounts(sgConnection, A2ARegistrationType.Vault);

                        var accountsToPush = secretsBrokerAccounts.Where(x => a2aAccounts.All(y => !y.AccountName.Equals(x.AccountName, StringComparison.InvariantCultureIgnoreCase)))
                            .Select(x => new SppAccount() {Id = x.AccountId, Name = x.AccountName});

                        _safeguardLogic.AddA2ARetrievableAccounts(sgConnection, accountsToPush, A2ARegistrationType.Vault);
                    }
                }
            }
        }

        private void CheckAndAddSecretsBrokerInstance(ISafeguardConnection sgConnection)
        {
            if (_safeguardLogic.DevOpsSecretsBroker == null)
            {
                // This call just gets the latest Secrets Broker instance from SPP and caches it.
                _safeguardLogic.RetrieveDevOpsSecretsBrokerInstance(sgConnection);
            }

            if (_safeguardLogic.DevOpsSecretsBroker != null)
            {
                var needsUpdate = false;
                var devopsInstance = _safeguardLogic.DevOpsSecretsBroker;
                var devopsAsset = _safeguardLogic.DevOpsSecretsBroker.Asset ?? _safeguardLogic.GetAsset(sgConnection);
                var plugins = _configDb.GetAllPlugins();

                if (devopsAsset == null)
                {
                    // By setting the asset id to 0 and updating the devops instance, Safeguard will regenerate the asset.
                    devopsInstance.Asset.Id = 0;
                    needsUpdate = true;
                } 
                else if (_safeguardLogic.DevOpsSecretsBroker.Asset.Id != devopsAsset.Id)
                {
                    devopsInstance.Asset = devopsAsset;
                    needsUpdate = true;
                }

                if (plugins != null)
                {
                    var devopsPlugins = plugins.Select(x => x.ToDevOpsSecretsBrokerPlugin(_pluginsLogic)).ToList();
                    if (!devopsPlugins.SequenceEqual(devopsInstance.Plugins))
                    {
                        devopsInstance.Plugins = devopsPlugins;
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                    _safeguardLogic.UpdateSecretsBrokerInstance(sgConnection, devopsInstance);
            }
        }


        private void CheckAndSyncVaultCredentials(ISafeguardConnection sgConnection)
        {
            var addons = _configDb.GetAllAddons().ToList();
            if (!addons.Any())
                return;

            var a2aRegistration = _safeguardLogic.GetA2ARegistration(sgConnection, A2ARegistrationType.Vault);

            var accounts = new List<A2ARetrievableAccount>();

            var result = SafeguardLogic.DevOpsInvokeMethodFull(_configDb.SvcId, sgConnection, Service.Core, Method.Get, $"A2ARegistrations/{a2aRegistration.Id}/RetrievableAccounts");
            if (result.StatusCode == HttpStatusCode.OK)
            {
                accounts = JsonHelper.DeserializeObject<List<A2ARetrievableAccount>>(result.Body);
            }

            if (accounts.Any())
            {
                using var a2aContext = GetA2aContext();
                foreach (var account in accounts)
                {
                    string pp;
                    try
                    {
                        var p = a2aContext.RetrievePassword(account.ApiKey.ToSecureString());
                        pp = p.ToInsecureString();
                        if (string.IsNullOrEmpty(pp))
                            continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.Information(ex, $"Failed to check the password for account {account.AccountName} ");
                        continue;
                    }

                    foreach (var addon in addons)
                    {
                        var addonAccount =
                            addon.VaultCredentials.FirstOrDefault(x => account.AccountName.StartsWith(x.Key) && !pp.Equals(x.Value));
                        if (!string.IsNullOrEmpty(addonAccount.Value))
                        {
                            result = SafeguardLogic.DevOpsInvokeMethodFull(_configDb.SvcId, sgConnection, Service.Core, Method.Put, $"AssetAccounts/{account.AccountId}/Password", $"\"{addonAccount.Value}\"");
                            if (result.StatusCode != HttpStatusCode.OK)
                            {
                                _logger.Error($"Failed to sync the password for account {account.AccountName} ");
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
