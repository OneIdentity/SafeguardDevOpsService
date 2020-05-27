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
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet]
        public ActionResult<IEnumerable<Plugin>> GetPlugins([FromServices] IPluginsLogic pluginsLogic)
        {
            var plugins = pluginsLogic.GetAllPlugins();
            if (plugins == null)
                return NotFound();

            return Ok(plugins.ToArray());
        }

        /// <summary>
        /// Get the configuration for a specific plugin.
        /// </summary>
        /// <param name="name">Name of the plugin.</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("{name}")]
        public ActionResult<Plugin> GetPlugin([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            var plugin = pluginsLogic.GetPluginByName(name);
            if (plugin == null)
                return NotFound();

            return Ok(plugin);
        }

        /// <summary>
        /// Update the configuration for a plugin.
        /// </summary>
        /// <param name="pluginConfiguration">Object containing a JSON configuration string.</param>
        /// <param name="name">Name of plugin to update</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpPut("Plugins/{name}")]
        public ActionResult<Plugin> GetPlugins([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name, [FromBody] PluginConfiguration pluginConfiguration)
        {
            var plugin = pluginsLogic.SavePluginConfigurationByName(pluginConfiguration, name);
            if (plugin == null)
                return NotFound();

            return Ok(plugin);
        }

        /// <summary>
        /// Delete the configuration for a specific plugin.
        /// </summary>
        /// <param name="name">Name of the plugin.</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("{name}")]
        public ActionResult<Plugin> DeletePlugin([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            pluginsLogic.DeleteAccountMappings(name);
            pluginsLogic.DeletePluginByName(name);
        
            return Ok();
        }

        /// <summary>
        /// Get the list of accounts that are mapped to a vault plugin.
        /// </summary>
        /// <param name="name">Name of the plugin</param>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("{name}/Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> GetAccountMapping([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            var accountMappings = pluginsLogic.GetAccountMappings(name);

            return Ok(accountMappings);
        }

        /// <summary>
        /// Map a set of accounts to a vault plugin.
        /// </summary>
        /// <param name="name">Name of the plugin</param>
        /// <param name="accounts">List of accounts to be mapped</param>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("{name}/Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> AddAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name, IEnumerable<A2ARetrievableAccount> accounts)
        {
            var accountMappings = pluginsLogic.SaveAccountMappings(name, accounts);

            return Ok(accountMappings);
        }

        /// <summary>
        /// Delete all of the mapped accounts for a vault plugin.
        /// </summary>
        /// <param name="name">Name of the plugin</param>
        /// <response code="204">No Content</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("{name}/Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> DeleteAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            pluginsLogic.DeleteAccountMappings(name);

            return NoContent();
        }

        /// <summary>
        /// Delete all of the mapped accounts.  To help prevent unintended mapped accounts removal, the confirm query param is required.
        /// </summary>
        /// <param name="confirm">This query parameter must be set to "yes" if the caller intends to remove all of the account mappings.</param>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> DeleteAllAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromQuery] string confirm)
        {
            if (!confirm.Equals("yes", StringComparison.InvariantCultureIgnoreCase))
                return BadRequest();

            pluginsLogic.DeleteAccountMappings();

            return NoContent();
        }
    }
}
