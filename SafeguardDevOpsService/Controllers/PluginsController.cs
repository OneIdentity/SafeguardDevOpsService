using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Controllers
{
    [ApiController]
    [Route("service/devops/[controller]")]
    public class PluginsController : ControllerBase
    {
        private readonly Serilog.ILogger _logger;

        public PluginsController()
        {
            _logger = Serilog.Log.Logger;
        }

        /// <summary>
        /// Get a list of all registered plugins.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet]
        public ActionResult<IEnumerable<Plugin>> GetPlugins([FromServices] IPluginsLogic pluginsLogic)
        {
            var plugins = pluginsLogic.GetAllPlugins();
            if (plugins == null)
                return NotFound();

            return Ok(plugins.ToArray());
        }

        /// <summary>
        /// Get the information for a specific plugin.
        /// </summary>
        /// <param name="name">Name of the plugin.</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("{name}")]
        public ActionResult<Plugin> GetPlugin([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            var plugin = pluginsLogic.GetPluginByName(name);
            if (plugin == null)
                return NotFound();

            return Ok(plugin);
        }

        /// <summary>
        /// Delete the information for a specific plugin.
        /// </summary>
        /// <param name="name">Name of the plugin.</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpDelete("{name}")]
        public ActionResult<Plugin> DeletePlugin([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            pluginsLogic.DeletePluginByName(name);

            return Ok();
        }

        /*
        /// <summary>
        /// Update the configuration for a plugin.
        /// </summary>
        /// <param name="pluginConfiguration">Object containing a JSON configuration string.</param>
        /// <param name="name">Name of plugin to update</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpPut("{name}/SafeguardController")]
        public ActionResult<Plugin> GetPlugins([FromBody] PluginConfiguration pluginConfiguration, [FromRoute] string name)
        {
            var plugin = _configurationLogic.SavePluginConfigurationByName(pluginConfiguration, name);
            if (plugin == null)
                return NotFound();

            return Ok(plugin);
        }
        */
    }
}
