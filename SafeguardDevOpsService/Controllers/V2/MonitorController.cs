using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Attributes;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Logic;

#pragma warning disable 1573

namespace OneIdentity.DevOps.Controllers.V2
{
    /// <summary>
    /// APIs that get or change the state of the account monitor.
    /// </summary>
    [ApiController]
    [Route("service/devops/v2/[controller]")]
    public class MonitorController : ControllerBase
    {
        private readonly Serilog.ILogger _logger;

        /// <summary>
        ///
        /// </summary>
        public MonitorController()
        {
            _logger = Serilog.Log.Logger;
        }

        /// <summary>
        /// Get the current state of the A2A and Reverse Flow monitoring.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps monitors the associated Safeguard for Privileged Passwords appliance for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.
        ///
        /// This endpoint gets the current state of the A2A and Reverse Flow monitoring.
        /// </remarks>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet]
        public ActionResult<FullMonitorState> GetMonitorState([FromServices] IMonitoringLogic monitoringLogic)
        {
            var monitoring = monitoringLogic.GetFullMonitorState();

            return Ok(monitoring);
        }

        /// <summary>
        /// Force a new state of A2A and Reverse Flow monitoring.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps monitors the associated Safeguard for Privileged Passwords appliance for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.
        ///
        /// This endpoint forces a new state of both the A2A and Reverse Flow monitoring.
        /// </remarks>
        /// <param name="monitorState">New state of the monitor.</param>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost]
        public ActionResult<FullMonitorState> ForceMonitorState([FromServices] IMonitoringLogic monitoringLogic, MonitorState monitorState)
        {
            monitoringLogic.EnableMonitoring(monitorState.Enabled);

            return Ok(monitoringLogic.GetFullMonitorState());
        }

        /// <summary>
        /// Set the state of the A2A or the Reverse Flow monitoring.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps monitors the associated Safeguard for Privileged Passwords appliance for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.
        ///
        /// This endpoint sets a new state of the A2A or Reverse Flow monitoring.
        /// </remarks>
        /// <param name="fullMonitorState">New state of the monitor.</param>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPut]
        public ActionResult<FullMonitorState> SetMonitorState([FromServices] IMonitoringLogic monitoringLogic, FullMonitorState fullMonitorState)
        {
            monitoringLogic.EnableMonitoring(fullMonitorState);

            return Ok(monitoringLogic.GetFullMonitorState());
        }

        /// <summary>
        /// Get the last x number of credential push events. Default size is 25.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps monitors the associated Safeguard for Privileged Passwords appliance for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.
        ///
        /// This endpoint gets the last x number of credential push events.
        /// </remarks>
        /// <param name="size">Number of events to return.</param>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("Events")]
        public ActionResult<IEnumerable<MonitorEvent>> GetMonitorEvents([FromServices] IMonitoringLogic monitoringLogic, [FromQuery] int size = 25)
        {
            var monitorEvents = monitoringLogic.GetMonitorEvents(size);

            return Ok(monitorEvents);
        }

        /// <summary>
        /// Get the current state of the Reverse Flow monitor.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps reverser flow monitors the associated third-party vaults for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.
        ///
        /// This endpoint gets the current state of the Reverse Flow monitor.
        /// </remarks>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("ReverseFlow")]
        public ActionResult<ReverseFlowMonitorState> GetReverseFlowMonitorState([FromServices] IMonitoringLogic monitoringLogic)
        {
            var monitoring = monitoringLogic.GetReverseFlowMonitorState();

            return Ok(monitoring);
        }

        /// <summary>
        /// Set the current state of the Reverse Flow monitor.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps reverser flow monitors the associated third-party vaults for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.
        ///
        /// This endpoint sets the current state of the Reverse Flow monitor.
        /// </remarks>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPut("ReverseFlow")]
        public ActionResult<ReverseFlowMonitorState> SetReverseFlowMonitorState([FromServices] IMonitoringLogic monitoringLogic, ReverseFlowMonitorState reverseFlowMonitorState)
        {
            var monitoring = monitoringLogic.SetReverseFlowMonitorState(reverseFlowMonitorState);

            return Ok(monitoring);
        }

        /// <summary>
        /// Force a single Reverse Flow polling cycle.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps reverser flow monitors the associated third-party vaults for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.
        ///
        /// This endpoint forces a single Reverse Flow polling cycle outside of monitoring.
        /// </remarks>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("ReverseFlow/PollNow")]
        public ActionResult PollReverseFlow([FromServices] IMonitoringLogic monitoringLogic)
        {
            if (!monitoringLogic.PollReverseFlow())
                return BadRequest("Reverse flow monitoring not available.");

            return Ok();
        }
    }
}
