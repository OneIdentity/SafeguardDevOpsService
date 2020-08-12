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
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin must be installed and configured individually.
        ///
        /// This endpoint lists all of the plugins that have been install along with the specific configuration requirements.
        /// </remarks>
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
        /// Upload a new plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin must be installed and configured individually.
        ///
        /// The plugin must be a zip compressed file that has been converted to a base64 string.&lt;br /&gt;
        /// Powershell example:&lt;br /&gt;
        ///   $fileContentBytes = get-content 'plugin-zip-file' -Encoding Byte&lt;br /&gt;
        ///   [System.Convert]::ToBase64String($fileContentBytes) | Out-File 'plugin-text-file.txt'&lt;br /&gt;
        ///
        /// Each plugin is installed into the \ProgramData\SafeguardDevOpsService\ExternalPlugins folder.
        ///
        /// (See POST /service/devops/{version}/Plugins/File to upload a plugin file using multipart-form-data)
        /// </remarks>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost]
        public ActionResult UploadPlugin([FromServices] IPluginsLogic pluginsLogic, Plugin pluginInfo, [FromQuery] bool restart = false)
        {
            pluginsLogic.InstallPlugin(pluginInfo.Base64PluginData);

            if (restart)
                pluginsLogic.RestartService();
            else if (RestartManager.Instance.ShouldRestart)
                return Ok("Safeguard Secrets Broker for DevOps needs to be restarted to finish installing the new plugin.");

            return NoContent();
        }

        /// <summary>
        /// Upload a new plugin via multipart-form-data.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin must be installed and configured individually.
        ///
        /// The plugin must be a zip compressed. Each plugin is installed into the \ProgramData\SafeguardDevOpsService\ExternalPlugins folder.
        /// </remarks>
        /// <param name="formFile">Zip compressed plugin file.</param>
        /// <param name="restart">Restart Safeguard Secrets Broker for DevOps after plugin install.</param>
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
            else if (RestartManager.Instance.ShouldRestart)
                return Ok("Safeguard Secrets Broker for DevOps needs to be restarted to finish installing the new plugin.");

            return NoContent();
        }

        /// <summary>
        /// Get the configuration for a specific plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin must be installed and configured individually.
        ///
        /// This endpoint gets the configuration for a specific plugin by name.
        /// </remarks>
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
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin must be installed and configured individually.
        ///
        /// This endpoint sets the configuration for a specific plugin by name.
        /// </remarks>
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
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin must be installed and configured individually.
        ///
        /// This endpoint removes the configuration for a specific plugin by name and unregisters the plugin from Safeguard Secrets Broker for DevOps.
        /// However, this endpoint does not remove the plugin from the \ProgramData\SafeguardDevOpsService\ExternalPlugins folder. The
        /// plugin files must be manually removed from the ExternalPlugins folder once Safeguard Secrets Broker for DevOps has been stopped.
        /// </remarks>
        /// <param name="name">Name of the plugin.</param>
        /// <response code="204">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("{name}")]
        public ActionResult DeletePlugin([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            pluginsLogic.DeleteAccountMappings(name);
            pluginsLogic.RemovePluginVaultAccount(name);
            pluginsLogic.DeletePluginByName(name);
        
            return NoContent();
        }

        /// <summary>
        /// Get the list of accounts that are mapped to a vault plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Accounts must be mapped to each plugin so that the corresponding credential can be pushed to the third
        /// party vault. By mapping an account to a plugin, Safeguard Secrets Broker for DevOps monitor will recognize that any password change for
        /// the mapped account, should be pushed to the plugin.
        ///
        /// This endpoint gets a list of accounts that have been mapped to the specified plugin.
        /// </remarks>
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
        /// Get an account that is mapped to a vault plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Accounts must be mapped to each plugin so that the corresponding credential can be pushed to the third
        /// party vault. By mapping an account to a plugin, Safeguard Secrets Broker for DevOps monitor will recognize that any password change for
        /// the mapped account, should be pushed to the plugin.
        ///
        /// This endpoint gets an account that has been mapped to the specified plugin.
        /// </remarks>
        /// <param name="name">Name of the plugin</param>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("{name}/Accounts/{accountId}")]
        public ActionResult<AccountMapping> GetAccountMappingById([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name, [FromRoute] int accountId)
        {
            var accountMapping = pluginsLogic.GetAccountMappingById(name, accountId);
            if (accountMapping == null)
                return NotFound();

            return Ok(accountMapping);
        }

        /// <summary>
        /// Map a set of accounts to a vault plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Accounts must be mapped to each plugin so that the corresponding credential can be pushed to the third
        /// party vault. By mapping an account to a plugin, Safeguard Secrets Broker for DevOps monitor will recognize that any password change for
        /// the mapped account, should be pushed to the plugin.
        ///
        /// This endpoint maps a list of accounts to the specified plugin.
        /// </remarks>
        /// <param name="name">Name of the plugin</param>
        /// <param name="accounts">List of accounts to be mapped</param>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPut("{name}/Accounts")]
        public ActionResult<IEnumerable<AccountMapping>> AddAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name, IEnumerable<A2ARetrievableAccount> accounts)
        {
            var accountMappings = pluginsLogic.SaveAccountMappings(name, accounts);

            return Ok(accountMappings);
        }

        /// <summary>
        /// Remove a set of accounts or all accounts from a vault plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Accounts must be mapped to each plugin so that the corresponding credential can be pushed to the third
        /// party vault. By mapping an account to a plugin, Safeguard Secrets Broker for DevOps monitor will recognize that any password change for
        /// the mapped account, should be pushed to the plugin.
        ///
        /// This endpoint removes a list of accounts from the specified plugin. If query param removeAll is set to true, the body will
        /// be ignored and all accounts that are mapped to the plugin will be removed.
        /// </remarks>
        /// <param name="name">Name of the plugin</param>
        /// <param name="accounts">List of accounts to be mapped</param>
        /// <param name="removeAll">Remove all mapped accounts for the plugin. If set to true, the body will be ignored.</param>
        /// <response code="204">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("{name}/Accounts")]
        public ActionResult RemoveAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name, 
            [FromBody] IEnumerable<AccountMapping> accounts, [FromQuery] bool removeAll = false)
        {
            if (removeAll)
                pluginsLogic.DeleteAccountMappings(name);
            else
            {
                pluginsLogic.DeleteAccountMappings(name, accounts);
            }

            return NoContent();
        }

        /// <summary>
        /// Delete all of the mapped accounts.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Accounts must be mapped to each plugin so that the corresponding credential can be pushed to the third
        /// party vault. By mapping an account to a plugin, Safeguard Secrets Broker for DevOps monitor will recognize that any password change for
        /// the mapped account, should be pushed to the plugin.
        ///
        /// This endpoint removes all of the mapped accounts from all of the registered plugin.
        /// 
        /// To help prevent unintended Safeguard appliance connection removal, the confirm query param is required and must be set to "yes".
        /// </remarks>
        /// <param name="confirm">This query parameter must be set to "yes" if the caller intends to remove all of the account mappings.</param>
        /// <response code="204">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("Accounts")]
        public ActionResult DeleteAllAccountMappings([FromServices] IPluginsLogic pluginsLogic, [FromQuery] string confirm)
        {
            if (confirm == null || !confirm.Equals("yes", StringComparison.InvariantCultureIgnoreCase))
                return BadRequest();

            pluginsLogic.DeleteAccountMappings();

            return NoContent();
        }

        /// <summary>
        /// Get the vault account that is associated with a specific plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin usually has a credential that is used to authenticate to the third party vault. This credential
        /// must be stored in the Safeguard for Privileged Passwords appliance and fetched at the time when Safeguard Secrets Broker for DevOps needs
        /// to authenticate to the third party vault.
        ///
        /// This endpoint gets the Safeguard for Privileged Passwords asset/account that has been mapped to a plugin.
        /// </remarks>
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
        /// Map an account with the vault credential to a plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin usually has a credential that is used to authenticate to the third party vault. This credential
        /// must be stored in the Safeguard for Privileged Passwords appliance and fetched at the time when Safeguard Secrets Broker for DevOps needs
        /// to authenticate to the third party vault.
        ///
        /// This endpoint maps a Safeguard for Privileged Passwords asset/account to a plugin.
        ///
        /// (See /service/devops/Safeguard/AvailableAccounts)
        /// </remarks>
        /// <param name="name">Name of plugin to update</param>
        /// <param name="assetAccount">Account to associate with the vault.</param>
        /// <response code="200">Success</response>
        /// <response code="400">Not found</response>
        [HttpPut("{name}/VaultAccount")]
        public ActionResult<AssetAccount> PutPluginVaultAccount([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name, [FromBody] AssetAccount assetAccount)
        {
            var account = pluginsLogic.SavePluginVaultAccount(name, assetAccount);

            return Ok(account);
        }

        /// <summary>
        /// Remove a mapped vault credential account from a plugin.
        /// </summary>
        /// <remarks>
        /// Safeguard Secrets Broker for DevOps uses individualized plugins that are capable of pushing credential information to a specific third
        /// party vault. Each plugin usually has a credential that is used to authenticate to the third party vault. This credential
        /// must be stored in the Safeguard for Privileged Passwords appliance and fetched at the time when Safeguard Secrets Broker for DevOps needs
        /// to authenticate to the third party vault.
        ///
        /// This endpoint removes a Safeguard for Privileged Passwords asset/account for a plugin.
        ///
        /// (See /service/devops/Safeguard/AvailableAccounts)
        /// </remarks>
        /// <param name="name">Name of plugin to update</param>
        /// <response code="204">Success</response>
        /// <response code="400">Bad Request</response>
        [HttpDelete("{name}/VaultAccount")]
        public ActionResult RemovePluginVaultAccount([FromServices] IPluginsLogic pluginsLogic, [FromRoute] string name)
        {
            pluginsLogic.RemovePluginVaultAccount(name);

            return NoContent();
        }
    }
}
