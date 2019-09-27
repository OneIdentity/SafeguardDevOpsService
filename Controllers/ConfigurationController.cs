using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.SafeguardDevOpsService.ConfigDb;
using OneIdentity.SafeguardDevOpsService.ConfigurationImpl;
using OneIdentity.SafeguardDevOpsService.Data;
using OneIdentity.SafeguardDevOpsService.Impl;

namespace OneIdentity.SafeguardDevOpsService.Controllers
{

    [Controller]
    [Route("devops/[controller]")]
    public class ConfigurationController : Controller
    {
        private readonly IConfigurationRepository _configurationRepository;
        private readonly IConfigurationLogic _configurationLogic;

        public ConfigurationController(IConfigurationRepository configurationRepository, IConfigurationLogic configurationLogic)
        {
            _configurationRepository = configurationRepository;
            _configurationLogic = configurationLogic;
        }

        /// <summary>
        /// Get the current configuration of the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet]
        public ActionResult<Configuration> GetConfiguration()
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration == null)
                return NotFound();

            return Ok(configuration);
        }

        /// <summary>
        /// Configure the DevOps service for the first time.  The API assumes that a certificate user and A2A
        /// registration has been defined on the SPS appliance and that the certificate and private key have
        /// been imported into the Windows certificate on the appliances that is running the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [HttpPost]
        public ActionResult<Configuration> PostConfiguration([FromBody]InitialConfiguration initialConfig)
        {
            var configuration = _configurationRepository.GetConfiguration();
            if (configuration != null)
                return BadRequest("DevOps service has already been configured.");

            configuration = _configurationLogic.InitialConfiguration(initialConfig);
            return Ok(configuration);
        }

        /// <summary>
        /// Completely deletes the current configuration.
        /// </summary>
        /// <response code="200">Success</response>
        [HttpDelete]
        public ActionResult DeleteConfiguration()
        {
            _configurationLogic.DeleteConfiguration();
            return Ok();
        }

        /// <summary>
        /// Get the current A2A registration information.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("Registration")]
        public ActionResult<Registration> GetRegistration()
        {
            var registration = _configurationLogic.GetRegistration();
            if (registration == null)
                return NotFound();

            return Ok(registration);
        }

        /// <summary>
        /// Updates the network address and user thumbprint.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        /// <response code="404">Not found</response>
        [HttpPut("Connection")]
        public ActionResult<Configuration> PutConnectionConfiguration([FromBody]ConnectionConfiguration connectionConfig)
        {
            var configuration = _configurationLogic.UpdateConnectionConfiguration(connectionConfig);
            if (configuration == null)
                return NotFound();

            return Ok(configuration);
        }

        /// <summary>
        /// Get the list of accounts and mapped vault names.
        /// </summary>
        /// <param name="accountName">Filter the results by matching accountName.</param>
        /// <param name="vaultName">Filter the results by matching vaultName</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("AccountMapping")]
        public ActionResult<IEnumerable<AccountMapping>> GetAccountMapping([FromQuery] string accountName, [FromQuery] string vaultName)
        {
            var accountMappings = _configurationLogic.GetAccountMappings(accountName, vaultName);
            if (accountMappings == null)
                return NotFound();

            return Ok(accountMappings.ToArray());
        }

        /// <summary>
        /// Add non duplicate account mappings to the list.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpPut("AccountMapping")]
        public ActionResult<IEnumerable<AccountMapping>> PutAccountMapping([FromBody]IEnumerable<AccountMapping> newAccountMappings)
        {
            var accountMappings = _configurationLogic.SaveAccountMappings(newAccountMappings);
            return Ok(accountMappings);
        }

        /// <summary>
        /// Remove matching account mappings from the list. (Case sensitive)
        /// If there is no accountName and/or vaultName and removeAll is true, remove all account mappings.
        /// If there is an accountName and no vaultName then remove all matching account mappings for accountName.
        /// If there is a vaultName and no accountName then remove all matching account mappings for vaultName.
        /// If there is an accountName and a vaultName then remove the matching account mapping.
        /// </summary>
        /// <param name="removeAll">Remove all account mappings.</param>
        /// <param name="accountName">Filter the results by matching accountName.</param>
        /// <param name="vaultName">Filter the results by matching vaultName</param>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        /// <response code="404">Not found</response>
        [HttpDelete("AccountMapping")]
        public ActionResult<IEnumerable<AccountMapping>> PutAccountMapping([FromQuery] bool removeAll, [FromQuery] string accountName, [FromQuery] string vaultName)
        {
            var accountMappings = _configurationLogic.RemoveAccountMappings(removeAll, accountName, vaultName);
            return Ok(accountMappings);
        }

        /// <summary>
        /// Get the list of requestable accounts and api keys.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("RetrievableAccounts")]
        public ActionResult<IEnumerable<RetrievableAccount>> GetRetrievableAccounts()
        {
            var retrievableAccounts = _configurationLogic.GetRetrievableAccounts();
            if (retrievableAccounts == null)
                return NotFound();

            return Ok(retrievableAccounts.ToArray());
        }

        /// <summary>
        /// Enable or disable monitoring for password changes.  When monitoring is enabled, all retrievable accounts
        /// will be monitored for password changes.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [HttpPost("Monitoring")]
        public ActionResult<bool> PostMonitoring([FromQuery]bool enable = true)
        {
            _configurationLogic.EnableMonitoring(enable);
            return Ok();
        }

        /// <summary>
        /// Get a list of all registered plugins.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("Plugins")]
        public ActionResult<IEnumerable<Plugin>> GetPlugins()
        {
            //TODO: Get the list of registered plugins
            //TODO: Registering a plugin happens somewhere else
            //TODO:
            var setting = _configurationRepository.GetSetting(WellKnownData.PluginsName);
            if (setting == null)
                return NotFound();

            var plugins = JsonHelper.DeserializeObject<IEnumerable<Plugin>>(setting.Value);
            return plugins.ToArray();
        }

        /// <summary>
        /// Get the information for a specific plugin.
        /// </summary>
        /// <param name="name">Name of the plugin.
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpGet("Plugins/{name}")]
        public ActionResult<Plugin> GetPlugin([FromRoute] string name)
        {
            //TODO: Get the list of registered plugins
            //TODO: Find the plugin that matches the name
            //TODO:
            var setting = _configurationRepository.GetSetting(WellKnownData.PluginsName);
            if (setting == null)
                return NotFound();

            var plugins = JsonHelper.DeserializeObject<IEnumerable<Plugin>>(setting.Value);
            return plugins.FirstOrDefault();
        }

        /// <summary>
        /// Update the configuration for a plugin.
        /// </summary>
        /// <param name="PluginConfiguration">Object containing a JSON configuration string.</param>
        /// <param name="name">Name of plugin to update</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [HttpPut("Plugins/{name}/Configuration")]
        public ActionResult<Plugin> GetPlugins([FromBody] PluginConfiguration pluginConfiguration, [FromRoute] string name)
        {
            //TODO: Get the list of registered plugins
            //TODO: Find the plugin that matches the name
            //TODO: Replace the configuration with the new configuration
            //TODO:
            var setting = _configurationRepository.GetSetting(WellKnownData.PluginsName);
            if (setting == null)
                return NotFound();

            var plugins = JsonHelper.DeserializeObject<IEnumerable<Plugin>>(setting.Value);
            return plugins.FirstOrDefault();
        }

    }
}
