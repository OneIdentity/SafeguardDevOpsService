using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Attributes;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Logic;
using OneIdentity.SafeguardDotNet;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;
using Safeguard = OneIdentity.DevOps.Data.Safeguard;

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
        public ActionResult<Safeguard> GetSafeguard([FromServices] ISafeguardLogic safeguard)
        {
            var safeguardConnection = safeguard.GetSafeguardConnection();

            // var availability = safeguard.GetSafeguardData();
            // if (availability == null)
            //     return NotFound("No Safeguard has not been configured");
            return Ok(safeguardConnection);
            // TODO: error handling?
        }

        /// <summary>
        /// Configure a Safeguard configuration for the DevOps service to use.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [UnhandledExceptionError]
        [HttpPut]
        public ActionResult<Safeguard> SetSafeguard([FromServices] ISafeguardLogic safeguard,
            [FromBody] SafeguardData safeguardData)
        {
            var appliance = safeguard.SetSafeguardData(safeguardData);

            return Ok(appliance);
        }

        // /// <summary>
        // /// Deletes the current Safeguard configuration so that none is in use with the DevOps service.
        // /// </summary>
        // /// <response code="204">Success</response>
        // [UnhandledExceptionError]
        // [HttpDelete]
        // public ActionResult DeleteSafeguard([FromServices] ISafeguardLogic safeguard)
        // {
        //     safeguard.DeleteSafeguardData();
        //     return NoContent();
        //     // TODO: error handling?
        // }

        /// <summary>
        /// Configure a Safeguard configuration for the DevOps service to use.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("Configuration")]
        public ActionResult<ManagementConnection> GetDevOpsConfiguration([FromServices] ISafeguardLogic safeguard)
        {
            var managementConnection = safeguard.GetDevOpsConfiguration();

            return Ok(managementConnection);
        }

        /// <summary>
        /// Configure a Safeguard configuration for the DevOps service to use.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("Configuration")]
        public ActionResult<ManagementConnection> ConfigureSafeguard([FromServices] ISafeguardLogic safeguard, ClientCertificate certFile = null)
        {
            if (certFile != null && certFile.Base64CertificateData != null)
            {
                safeguard.InstallClientCertificate(certFile);
            }

            var devOpsConfiguration = safeguard.ConfigureDevOpsService();

            return Ok(devOpsConfiguration);
        }

        /// <summary>
        /// Configure a Safeguard configuration for the DevOps service to use.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("Configuration")]
        public ActionResult<ManagementConnection> DeleteSafeguardConfiguration([FromServices] ISafeguardLogic safeguard)
        {
            safeguard.ConfigureDevOpsService();

            return NoContent();
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
            var availability = safeguard.GetSafeguardConnection();
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

        /// <summary>
        /// Get the requestable accounts for the current user.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("AvailableAccounts")]
        public ActionResult<IEnumerable<SppAccount>> GetAvailableAccounts([FromServices] ISafeguardLogic safeguard)
        {
            var availableAccounts = safeguard.GetAvailableAccounts();

            return Ok(availableAccounts);
        }

        /// <summary>
        /// Get the requestable accounts for the current user.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("A2ARegistration")]
        public ActionResult<A2ARegistration> GetA2ARegistration([FromServices] ISafeguardLogic safeguard)
        {
            var registration = safeguard.GetA2ARegistration();
            if (registration == null)
                return NotFound();

            return Ok(registration);
        }

        /// <summary>
        /// Get the requestable accounts for the current user.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("A2ARegistration")]
        public ActionResult<A2ARegistration> DeleteA2ARegistration([FromServices] ISafeguardLogic safeguard, [FromQuery] string confirm)
        {
            if (confirm == null || !confirm.Equals("yes", StringComparison.InvariantCultureIgnoreCase))
                return BadRequest();

            safeguard.DeleteA2ARegistration();

            return NoContent();
        }

        /// <summary>
        /// Get the requestable accounts for the current user.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("A2ARegistration/RetrievableAccounts")]
        public ActionResult<IEnumerable<A2ARetrievableAccount>> GetRetrievableAccounts([FromServices] ISafeguardLogic safeguard)
        {
            var retrievableAccounts = safeguard.GetA2ARetrievableAccounts();

            return Ok(retrievableAccounts);
        }

        /// <summary>
        /// Get the requestable accounts for the current user.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("A2ARegistration/RetrievableAccounts")]
        public ActionResult<IEnumerable<A2ARetrievableAccount>> AddRetrievableAccounts([FromServices] ISafeguardLogic safeguard, IEnumerable<SppAccount> accounts)
        {
            var retrievableAccounts = safeguard.AddA2ARetrievableAccounts(accounts);

            return Ok(retrievableAccounts);
        }

    }
}
