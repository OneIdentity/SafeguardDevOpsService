using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Logic;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Controllers
{
    [Controller]
    [Route("service/devops/[controller]")]
    public class SafeguardConnectionController : Controller
    {
        private readonly Serilog.ILogger _logger;

        public SafeguardConnectionController()
        {
            _logger = Serilog.Log.Logger;
        }

        [HttpPut]
        public ActionResult<SafeguardConnection> TestSafeguardConnection([FromBody] SafeguardConnectionRequest connectionRequest)
        {
            ISafeguardConnection sg = null;
            SafeguardConnection connectionData = new SafeguardConnection();
            try
            {
                if (connectionRequest.AccessToken != null)
                {
                    connectionData.AccessToken = connectionRequest.AccessToken.ToSecureString();
                    sg = Safeguard.Connect(connectionRequest.NetworkAddress, connectionData.AccessToken,
                        ignoreSsl: connectionRequest.IgnoreSsl);
                    var meJson = sg.InvokeMethod(Service.Core, Method.Get, "Me");
                    var loggedInUser = JsonHelper.DeserializeObject<LoggedInUser>(meJson);
                    connectionData.IdentityProviderName = loggedInUser.IdentityProviderName;
                    connectionData.UserName = loggedInUser.UserName;
                    connectionData.AdminRoles = loggedInUser.AdminRoles;
                }
                else
                {
                    sg = Safeguard.Connect(connectionRequest.NetworkAddress, ignoreSsl: connectionRequest.IgnoreSsl);
                }

                var availabilityJson = sg.InvokeMethod(Service.Notification, Method.Get, "Status/Availability");
                var applianceAvailability = JsonHelper.DeserializeObject<ApplianceAvailability>(availabilityJson);
                connectionData.ApplianceId = applianceAvailability.ApplianceId;
                connectionData.ApplianceName = applianceAvailability.ApplianceName;
                connectionData.ApplianceVersion = applianceAvailability.ApplianceVersion;
                if (applianceAvailability.ApplianceCurrentState != "Online")
                {
                    // TODO: return error?
                }
                return connectionData;
            }
            catch (SafeguardDotNetException ex)
            {
                _logger.Error($"Failed to initialize the DevOps Service: {ex.Message}");
                return null; // TODO: return error?
            }
            finally
            {
                sg?.Dispose();
            }
        }

    }
}
