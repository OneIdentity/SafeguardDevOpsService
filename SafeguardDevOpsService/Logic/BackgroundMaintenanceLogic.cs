using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.SafeguardDotNet;

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
                    _logger.Debug("Connecting to Safeguard: {address}", sppAddress);
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
                if (!_safeguardLogic.PauseBackgroundMaintenance)
                {
                    try
                    {
                        using var sgConnection = GetSgConnection();

                        if (sgConnection != null)
                        {
                            var isLicensed = _safeguardLogic.ValidateLicense();

                            _safeguardLogic.CheckAndSyncSecretsBrokerInstance(sgConnection);
                            _safeguardLogic.CheckAndPushAddOnCredentials(sgConnection);
                            _safeguardLogic.CheckAndConfigureAddonPlugins(sgConnection, _pluginsLogic, !isLicensed);
                            _safeguardLogic.CheckAndSyncVaultCredentials(sgConnection);
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

        public void Dispose()
        {
        }
    }
}
