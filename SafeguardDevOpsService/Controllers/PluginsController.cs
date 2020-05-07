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

        /// <summary>
        /// Get the list of accounts and mapped vault names.
        /// </summary>
        /// <param name="name">Name of the plugin</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [HttpGet("{name}/Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> GetAccountMapping([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            var accountMappings = pluginsLogic.GetAccountMappings(name);

            return Ok(accountMappings);
        }

        /// <summary>
        /// Get the list of accounts and mapped vault names.
        /// </summary>
        /// <param name="name">Name of the plugin</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [HttpPost("{name}/Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> AddAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name, IEnumerable<A2ARetrievableAccount> accounts)
        {
            var accountMappings = pluginsLogic.SaveAccountMappings(name, accounts);

            return Ok(accountMappings);
        }

        /// <summary>
        /// Get the list of accounts and mapped vault names.
        /// </summary>
        /// <param name="name">Name of the plugin</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [HttpDelete("{name}/Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> DeleteAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            pluginsLogic.DeleteAccountMappings(name);

            return NoContent();
        }

        /// <summary>
        /// Get the list of accounts and mapped vault names.
        /// </summary>
        /// <param name="name">Name of the plugin</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [HttpDelete("Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> DeleteAllAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromQuery] string confirm)
        {
            if (!confirm.Equals("yes", StringComparison.InvariantCultureIgnoreCase))
                return BadRequest();

            pluginsLogic.DeleteAccountMappings();

            return NoContent();
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
