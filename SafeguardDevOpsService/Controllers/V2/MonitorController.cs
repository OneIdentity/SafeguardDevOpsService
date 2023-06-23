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
        /// Get the current state of the A2A monitor.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps monitors the associated Safeguard for Privileged Passwords appliance for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.  
        ///
        /// This endpoint gets the current state of the account monitor.
        /// </remarks>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet]
        public ActionResult<MonitorState> GetMonitor([FromServices] IMonitoringLogic monitoringLogic)
        {
            var monitoring = monitoringLogic.GetMonitorState();

            return Ok(monitoring);
        }

        /// <summary>
        /// Set the state of the A2A monitor.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps monitors the associated Safeguard for Privileged Passwords appliance for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.  
        ///
        /// This endpoint starts or stops the account monitor.
        /// </remarks>
        /// <param name="monitorState">New state of the monitor.</param>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost]
        public ActionResult<MonitorState> GetMonitor([FromServices] IMonitoringLogic monitoringLogic, MonitorState monitorState)
        {
            monitoringLogic.EnableMonitoring(monitorState.Enabled);

            return Ok(monitorState);
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
        /// Start a reverse flow polling cycle.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps monitors the associated Safeguard for Privileged Passwords appliance for any password change to any account that
        /// has been registered with Safeguard Secrets Broker for DevOps.  
        ///
        /// This endpoint starts a reverse flow polling cycle outside of monitoring.
        /// </remarks>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("ReverseFlow")]
        public ActionResult PollReverseFlow([FromServices] IMonitoringLogic monitoringLogic)
        {
            if (!monitoringLogic.PollReverseFlow())
                return BadRequest("Reverse flow monitoring not available.");

            return Ok();
        }
    }
}
