using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Attributes;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Logic;

#pragma warning disable 1573

namespace OneIdentity.DevOps.Controllers.V1
{
    /// <summary>
    /// 
    /// </summary>
    [ApiController]
    [Route("service/devops/v1/[controller]")]
    public class PluginsController : ControllerBase
    {
        private readonly Serilog.ILogger _logger;

        /// <summary>
        /// 
        /// </summary>
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
        /// Upload a new plugin.  The plugin must be a zip compressed file that has been converted to a base64 string.\n
        /// Powershell example:\n
        /// $fileContentBytes = get-content 'plugin-zip-file' -Encoding Byte
        /// [System.Convert]::ToBase64String($fileContentBytes) | Out-File 'plugin-text-file.txt'
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost]
        public ActionResult UploadPlugin([FromServices] IPluginsLogic pluginsLogic, Plugin pluginInfo)
        {
            pluginsLogic.InstallPlugin(pluginInfo.Base64PluginData);

            return NoContent();
        }

        /// <summary>
        /// Upload a new plugin via multipartformdata.
        /// </summary>
        /// <response code="200">Success. Needing restart</response>
        /// <response code="204">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("File")]
        public ActionResult UploadPlugin([FromServices] IPluginsLogic pluginsLogic, IFormFile formFile, [FromQuery] bool restart = false)
        {
            pluginsLogic.InstallPlugin(formFile);

            if (restart)
                pluginsLogic.RestartService();

            if (RestartManager.Instance.ShouldRestart)
                return Ok("The DevOps Service needs to be restarted to finish installing the new plugin.");

            return NoContent();
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
        [HttpPut("{name}")]
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

        /// <summary>
        /// Get the vault account that is associated with a specific plugin.
        /// </summary>
        /// <param name="name">Name of the plugin.</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("{name}/VaultAccount")]
        public ActionResult<AssetAccount> GetPluginVaultAccount([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            var account = pluginsLogic.GetPluginVaultAccount(name);
            if (account == null)
                return NotFound();

            return Ok(account);
        }

        /// <summary>
        /// Associate an account with a plugin. The associated account will provide the vault with the authentication credential. (See /service/devops/Safeguard/AvailableAccounts)
        /// </summary>
        /// <param name="name">Name of plugin to update</param>
        /// <param name="assetAccount">Account to associate with the vault.</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpPut("{name}/VaultAccount")]
        public ActionResult<AssetAccount> PutPluginVaultAccount([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name, [FromBody] AssetAccount assetAccount)
        {
            var account = pluginsLogic.SavePluginVaultAccount(name, assetAccount);
            if (account == null)
                return NotFound();

            return Ok(account);
        }
    }
}
