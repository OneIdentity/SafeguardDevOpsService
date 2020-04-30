using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Attributes;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Controllers
{
    [ApiController]
    [Route("service/devops/[controller]")]
    public class SafeguardController : ControllerBase
    {
        private readonly Serilog.ILogger _logger;

        public SafeguardController()
        {
            _logger = Serilog.Log.Logger;
        }

        /// <summary>
        /// Get the current Safeguard configuration to use with the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet]
        public ActionResult<ManagementConnection> GetSafeguard([FromServices] ISafeguardLogic safeguard)
        {
            var managementConnection = safeguard.GetConnection();

            // var availability = safeguard.GetSafeguardData();
            // if (availability == null)
            //     return NotFound("No Safeguard has not been configured");
            return Ok(managementConnection);
            // TODO: error handling?
        }

        /// <summary>
        /// Configure a Safeguard configuration for the DevOps service to use.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPut]
        public ActionResult<ManagementConnection> SetSafeguard([FromServices] ISafeguardLogic safeguard,
            [FromBody] SafeguardData safeguardData, [FromQuery] bool configure = false)
        {
            var managementConnection = new ManagementConnection()
            {
                Appliance = safeguard.SetSafeguardData(safeguardData)
            };

            if (configure)
            {
                safeguard.ConfigureDevOpsService();
                managementConnection = safeguard.GetConnection();
            }

            return Ok(managementConnection);
        }

        /// <summary>
        /// Deletes the current Safeguard configuration so that none is in use with the DevOps service.
        /// </summary>
        /// <response code="204">Success</response>
        [UnhandledExceptionError]
        [HttpDelete]
        public ActionResult DeleteSafeguard([FromServices] ISafeguardLogic safeguard)
        {
            safeguard.DeleteSafeguardData();
            return NoContent();
            // TODO: error handling?
        }

        /// <summary>
        /// Get the current Safeguard configuration to use with the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardTokenAuthorization]
        [UnhandledExceptionError]
        [HttpGet("Logon")]
        public ActionResult<Safeguard> GetSafeguardLogon([FromServices] ISafeguardLogic safeguard)
        {
            var availability = safeguard.GetSafeguardData();
            if (availability == null)
                return NotFound("No Safeguard has not been configured");
            return Ok(availability);
            // TODO: error handling?
        }

        /// <summary>
        /// Get the current Safeguard configuration to use with the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("Logoff")]
        public ActionResult<Safeguard> GetSafeguardLogoff([FromServices] ISafeguardLogic safeguard)
        {
            var sessionKey = HttpContext.Items["session-key"].ToString();
            AuthorizedCache.Instance.Remove(sessionKey);

            return Ok();
        }

        /// <summary>
        /// Get an installed client certificate by thumbprint.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [UnhandledExceptionError]
        [HttpGet("ClientCertificate")]
        public ActionResult<ClientCertificate> GetClientCertificate([FromServices] ISafeguardLogic safeguard)
        {
            var certificate = safeguard.GetClientCertificate();
            if (certificate == null)
                return NotFound();

            return Ok(certificate);
        }

        /// <summary>
        /// Install a trusted certificate.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [UnhandledExceptionError]
        [HttpPost("ClientCertificate")]
        public ActionResult InstallClientCertificate([FromServices] ISafeguardLogic safeguard, ClientCertificate certFile)
        {
            safeguard.InstallClientCertificate(certFile);
            return Ok();
        }

        /// <summary>
        /// Remove an installed client certificate by thumbprint.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [UnhandledExceptionError]
        [HttpDelete("ClientCertificate")]
        public ActionResult RemoveClientCertificate([FromServices] ISafeguardLogic safeguard)
        {
            safeguard.RemoveClientCertificate();

            return NoContent();
        }

        /// <summary>
        /// Get a CSR can be signed and uploaded back to the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [UnhandledExceptionError]
        [HttpGet("CSR")]
        public ActionResult<string> GetClientCSR([FromServices] ISafeguardLogic safeguard, [FromQuery] int? size, [FromQuery] string subjectName)
        {
            var csr = safeguard.GetClientCSR(size, subjectName);
            return Ok(csr);
        }

        // /// <summary>
        // /// Get the current management connection to Safeguard in use by the DevOps service.
        // /// </summary>
        // /// <response code="200">Success</response>
        // /// <response code="404">Not found</response>
        // [UnhandledExceptionError]
        // [HttpGet("ManagementConnection")]
        // public ActionResult<ManagementConnection> ConnectSafeguard([FromServices] ISafeguardLogic safeguard)
        // {
        //     var connection = safeguard.GetConnection();
        //     if (connection == null)
        //         return NotFound("Safeguard is not connected");
        //     return Ok(connection);
        //     // TODO: error handling?
        // }
        //
        // /// <summary>
        // /// Create the management connection to Safeguard to use with the DevOps service.
        // /// </summary>
        // /// <response code="200">Success</response>
        // /// <response code="404">Not found</response>
        // [UnhandledExceptionError]
        // [HttpPut("ManagementConnection")]
        // public ActionResult<ManagementConnection> ConnectSafeguard([FromServices] ISafeguardLogic safeguard,
        //     [FromBody] ManagementConnectionData connectionData)
        // {
        //     var connection = safeguard.Connect(connectionData);
        //     return Ok(connection);
        //     // TODO: error handling?
        // }
        //
        // /// <summary>
        // /// Remove the management connection to Safeguard so that none is in use with the DevOps service.
        // /// </summary>
        // /// <response code="200">Success</response>
        // /// <response code="404">Not found</response>
        // [UnhandledExceptionError]
        // [HttpDelete("ManagementConnection")]
        // public ActionResult DisconnectSafeguard([FromServices] ISafeguardLogic safeguard)
        // {
        //     safeguard.Disconnect();
        //     return Ok();
        //     // TODO: error handling?
        // }
    }
}
