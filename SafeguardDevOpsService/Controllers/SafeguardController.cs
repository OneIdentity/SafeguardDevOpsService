using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Controllers
{
    [ApiController]
    [Route("service/devops/[controller]")]
    public class SafeguardController : Controller
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
        [HttpGet]
        public ActionResult<SafeguardAvailability> GetSafeguard([FromServices] ISafeguardLogic safeguard)
        {
            var availability = safeguard.GetSafeguardData();
            if (availability == null)
                return NotFound("No Safeguard has not been configured");
            return Ok(availability);
            // TODO: error handling?
        }

        /// <summary>
        /// Configure a Safeguard configuration for the DevOps service to use.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [HttpPut]
        public ActionResult<SafeguardAvailability> SetSafeguard([FromServices] ISafeguardLogic safeguard,
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
        /// Get the current management connection to Safeguard in use by the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("ManagementConnection")]
        public ActionResult<SafeguardConnection> ConnectSafeguard([FromServices] ISafeguardLogic safeguard)
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
        public ActionResult<SafeguardConnection> ConnectSafeguard([FromServices] ISafeguardLogic safeguard,
            [FromBody] SafeguardConnectionRequest connectionData)
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
