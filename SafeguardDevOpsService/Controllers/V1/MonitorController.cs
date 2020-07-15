using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Attributes;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Controllers.V1
{
    /// <summary>
    /// 
    /// </summary>
    [ApiController]
    [Route("service/devops/v1/[controller]")]
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
    }
}
