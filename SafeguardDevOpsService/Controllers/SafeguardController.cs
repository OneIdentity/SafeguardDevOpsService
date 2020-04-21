using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Authorization;
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
        [HttpGet]
        public ActionResult<Safeguard> GetSafeguard([FromServices] ISafeguardLogic safeguard)
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
        [SafeguardTokenAuthorization]
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
        [HttpGet("Logoff")]
        public ActionResult<Safeguard> GetSafeguardLogoff([FromServices] ISafeguardLogic safeguard)
        {
            var sessionKey = HttpContext.Items["session-key"].ToString();
            AuthorizedCache.Instance.Remove(sessionKey);

            return Ok();
        }

        /// <summary>
        /// Configure a Safeguard configuration for the DevOps service to use.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [HttpPut]
        public ActionResult<Safeguard> SetSafeguard([FromServices] ISafeguardLogic safeguard,
            [FromBody] SafeguardData safeguardData)
        {
            var availability = safeguard.SetSafeguardData(safeguardData);
            return Ok(availability);
            // TODO: error handling?
        }

        /// <summary>
        /// Deletes the current Safeguard configuration so that none is in use with the DevOps service.
        /// </summary>
        /// <response code="204">Success</response>
        [HttpDelete]
        public ActionResult DeleteSafeguard([FromServices] ISafeguardLogic safeguard)
        {
            safeguard.DeleteSafeguardData();
            return NoContent();
            // TODO: error handling?
        }

        /// <summary>
        /// Get an installed client certificate by thumbprint.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("ClientCertificate/{thumbPrint}")]
        public ActionResult<ClientCertificate> GetClientCertificate([FromServices] ISafeguardLogic safeguard, string thumbPrint)
        {
            var certificate = safeguard.GetClientCertificate(thumbPrint);
            if (certificate == null)
                return NotFound();

            return Ok(certificate);
        }

        /// <summary>
        /// Install a trusted certificate.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [HttpPost("ClientCertificate")]
        public ActionResult InstallClientCertificate([FromServices] ISafeguardLogic safeguard, [FromForm]ClientCertificatePfx certFile)
        {
            var size = certFile.file.Length;
            safeguard.InstallClientCertificate(certFile);
            return Ok();
        }

        /// <summary>
        /// Remove an installed client certificate by thumbprint.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpDelete("ClientCertificate/{thumbPrint}")]
        public ActionResult RemoveClientCertificate([FromServices] ISafeguardLogic safeguard, string thumbPrint)
        {
            safeguard.RemoveClientCertificate(thumbPrint);

            return NoContent();
        }

        /// <summary>
        /// Get the current management connection to Safeguard in use by the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("ManagementConnection")]
        public ActionResult<ManagementConnection> ConnectSafeguard([FromServices] ISafeguardLogic safeguard)
        {
            var connection = safeguard.GetConnection();
            if (connection == null)
                return NotFound("Safeguard is not connected");
            return Ok(connection);
            // TODO: error handling?
        }

        /// <summary>
        /// Create the management connection to Safeguard to use with the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpPut("ManagementConnection")]
        public ActionResult<ManagementConnection> ConnectSafeguard([FromServices] ISafeguardLogic safeguard,
            [FromBody] ManagementConnectionData connectionData)
        {
            var connection = safeguard.Connect(connectionData);
            return Ok(connection);
            // TODO: error handling?
        }

        /// <summary>
        /// Remove the management connection to Safeguard so that none is in use with the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpDelete("ManagementConnection")]
        public ActionResult DisconnectSafeguard([FromServices] ISafeguardLogic safeguard)
        {
            safeguard.Disconnect();
            return Ok();
            // TODO: error handling?
        }
    }
}
