using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Attributes;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Controllers
{
    [ApiController]
    [Route("service/devops/[controller]")]
    public class MonitorController : ControllerBase
    {
        private readonly Serilog.ILogger _logger;

        public MonitorController()
        {
            _logger = Serilog.Log.Logger;
        }

        /// <summary>
        /// Get a list of all registered plugins.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet]
        public ActionResult<MonitorState> GetMonitor([FromServices] IMonitoringLogic monitoringLogic)
        {
            var monitoring = monitoringLogic.GetMonitorState();

            return Ok(monitoring);
        }

        /// <summary>
        /// Get a list of all registered plugins.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
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
