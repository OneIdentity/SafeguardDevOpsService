using System;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using Safeguard = OneIdentity.DevOps.Data.Safeguard;

namespace OneIdentity.DevOps.Logic
{
    internal class SafeguardLogic : ISafeguardLogic, IDisposable
    {
        private const int DefaultApiVersion = 3;

        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;

        private ManagementConnection _connectionContext;

        public SafeguardLogic(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
        }

        private Safeguard GetSafeguardAvailability(ISafeguardConnection sg, ref Safeguard availability)
        {
            var availabilityJson = sg.InvokeMethod(Service.Notification, Method.Get, "Status/Availability");
            var applianceAvailability = JsonHelper.DeserializeObject<ApplianceAvailability>(availabilityJson);
            availability.ApplianceId = applianceAvailability.ApplianceId;
            availability.ApplianceName = applianceAvailability.ApplianceName;
            availability.ApplianceVersion = applianceAvailability.ApplianceVersion;
            availability.ApplianceState = applianceAvailability.ApplianceCurrentState;
            return availability;
        }

        private Safeguard ConnectAnonymous(string safeguardAddress, int apiVersion, bool ignoreSsl)
        {
            ISafeguardConnection sg = null;
            try
            {
                var availability = new Safeguard
                {
                    ApplianceAddress = safeguardAddress,
                    IgnoreSsl = ignoreSsl
                };
                sg = SafeguardDotNet.Safeguard.Connect(safeguardAddress, apiVersion, ignoreSsl);
                return GetSafeguardAvailability(sg, ref availability);
            }
            catch (SafeguardDotNetException ex)
            {
                _logger.Error($"Failed to contact Safeguard at '{safeguardAddress}': {ex.Message}");
                return null; // TODO: return error?
            }
            finally
            {
                sg?.Dispose();
            }
        }

        private void ConnectWithAccessToken(ManagementConnectionData connectionData)
        {
            if (_connectionContext != null)
            {
                DisconnectWithAccessToken();
            }

            ISafeguardConnection sg = null;
            try
            {
                if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                    return; // TODO: errors?
                if (string.IsNullOrEmpty(connectionData.AccessToken))
                    return; // TODO: errors?

                _connectionContext = new ManagementConnection
                {
                    AccessToken = connectionData.AccessToken.ToSecureString()
                };
                var availability = new Safeguard
                {
                    ApplianceAddress = _configDb.SafeguardAddress,
                    IgnoreSsl = connectionData.IgnoreSsl || (_configDb.IgnoreSsl ?? false)
                };
                sg = SafeguardDotNet.Safeguard.Connect(availability.ApplianceAddress, _connectionContext.AccessToken,
                    _configDb.ApiVersion ?? DefaultApiVersion, availability.IgnoreSsl);
                _connectionContext.Appliance = GetSafeguardAvailability(sg, ref availability);
                var meJson = sg.InvokeMethod(Service.Core, Method.Get, "Me");
                var loggedInUser = JsonHelper.DeserializeObject<LoggedInUser>(meJson);

                _connectionContext.IdentityProviderName = loggedInUser.IdentityProviderName;
                _connectionContext.UserName = loggedInUser.UserName;
                _connectionContext.AdminRoles = loggedInUser.AdminRoles;
            }
            catch (SafeguardDotNetException ex)
            {
                _logger.Error($"Failed to connect to Safeguard at '{_configDb.SafeguardAddress}': {ex.Message}");
                // TODO: return error?
            }
            finally
            {
                sg?.Dispose();
            }
        }

        private void DisconnectWithAccessToken()
        {
            _connectionContext?.AccessToken?.Dispose();
            _connectionContext = null;
        }
    

        public Safeguard GetSafeguardData()
        {
            if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                return null;
            return ConnectAnonymous(_configDb.SafeguardAddress, _configDb.ApiVersion ?? DefaultApiVersion, _configDb.IgnoreSsl ?? false);
        }

        public Safeguard SetSafeguardData(SafeguardData safeguardData)
        {
            var availability = ConnectAnonymous(safeguardData.NetworkAddress,
                safeguardData.ApiVersion ?? DefaultApiVersion, safeguardData.IgnoreSsl ?? false);

            if (availability != null)
            {
                _configDb.SafeguardAddress = safeguardData.NetworkAddress;
                _configDb.ApiVersion = safeguardData.ApiVersion ?? DefaultApiVersion;
                _configDb.IgnoreSsl = safeguardData.IgnoreSsl ?? false;
            }

            return availability;
        }

        public void DeleteSafeguardData()
        {
            _configDb.SafeguardAddress = null;
            _configDb.ApiVersion = null;
            _configDb.IgnoreSsl = null;
        }

        public ManagementConnection GetConnection()
        {
            return _connectionContext;
        }

        public ManagementConnection Connect(ManagementConnectionData connectionData)
        {
            ConnectWithAccessToken(connectionData);
            return _connectionContext;
            // TODO: errors?
        }

        public void Disconnect()
        {
            DisconnectWithAccessToken();
        }

        public void Dispose()
        {
            DisconnectWithAccessToken();
        }
    }
}
