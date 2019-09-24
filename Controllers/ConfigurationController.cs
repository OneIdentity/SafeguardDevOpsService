using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.SafeguardDevOpsService.ConfigDb;
using OneIdentity.SafeguardDevOpsService.Data;
using OneIdentity.SafeguardDevOpsService.Impl;

namespace OneIdentity.SafeguardDevOpsService.Controllers
{

    [Controller]
    [Route("devops/[controller]")]
    public class ConfigurationController : Controller
    {
        private readonly IConfigurationRepository _configurationRepository;

        public ConfigurationController(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository;
        }

        [HttpGet]
        public ActionResult<Configuration> GetConfiguration()
        {
            var setting = _configurationRepository.GetSetting(WellKnownData.ConfigurationName);
            if (setting == null)
                return NotFound();

            var config = JsonHelper.DeserializeObject<Configuration>(setting.Value);
            return config;
        }

        [HttpPost]
        public ActionResult<Configuration> PostConfiguration(InitialConfiguration initialConfig)
        {
            //TODO: Create a new configuration element here
            //TODO: Check to see if there is already a configuration.  If so, throw.
            //TODO: Upload the trusted certificate to SPP
            //TODO: Store the certificate and private key in the windows certificate store
            //TODO: Create a new certificate user with the thumb print from the trusted certificate
            //TODO: Create a new A2A registration with well known name and description
            //TODO: Add the account names to the A2A registration
            //TODO: Pull and cache the ApiKeys for the A2A accounts
            //TODO: Get the registration and store the configuration in the database
            //TODO:
            var setting = _configurationRepository.GetSetting(WellKnownData.ConfigurationName);
            if (setting == null)
                return NotFound();

            var config = JsonHelper.DeserializeObject<Configuration>(setting.Value);
            return config;
        }

        [HttpDelete]
        public void DeleteConfiguration()
        {
            //TODO: Delete the stored configuration and start clean
        }

        [HttpGet("Registration")]
        public ActionResult<Registration> GetRegistration()
        {
            var setting = _configurationRepository.GetSetting(WellKnownData.ConfigurationName);
            if (setting == null)
                return NotFound();

            var config = JsonHelper.DeserializeObject<Registration>(setting.Value);
            return config;
        }

        [HttpPut("Certificate")]
        public ActionResult<Configuration> PutClientCertificate(ClientCertificate cert)
        {
            //TODO: Update the new trusted certificate to SPP
            //TODO: Update the certificate user with the thumbprint of the new certificate
            //TODO: Remove the old certificate
            //TODO: Update the windows certificate store with the new certificate and private key
            //TODO: Update the thumbprint in the configuration
            //TODO:
            var setting = _configurationRepository.GetSetting(WellKnownData.ConfigurationName);
            if (setting == null)
                return NotFound();

            var config = JsonHelper.DeserializeObject<Configuration>(setting.Value);
            return config;
        }

        [HttpGet("AccountMapping")]
        public ActionResult<IEnumerable<AccountMapping>> GetAccountMapping([FromQuery] string filter)
        {
            //TODO: Check if there is a filter and if the contains AccountName and/or VaultName
            //TODO: Get all of the accout mappings and filter if necessary
            //TODO:
            var setting = _configurationRepository.GetSetting(WellKnownData.ConfigurationName);
            if (setting == null)
                return NotFound();

            var config = JsonHelper.DeserializeObject<Configuration>(setting.Value);
            return config.AccountMapping.ToArray();
        }

        [HttpPut("AccountMapping")]
        public ActionResult<IEnumerable<AccountMapping>> PutAccountMapping(IEnumerable<AccountMapping> accountMapping)
        {
            //TODO: Get the account mapping list
            //TODO: Add all non-duplicate account mappings
            //TODO: Save the new account mapping list to the database
            //TODO:
            var setting = _configurationRepository.GetSetting(WellKnownData.ConfigurationName);
            if (setting == null)
                return NotFound();

            var config = JsonHelper.DeserializeObject<Configuration>(setting.Value);
            return config.AccountMapping.ToArray();
        }

        [HttpDelete("AccountMapping")]
        public ActionResult<IEnumerable<AccountMapping>> PutAccountMapping([FromQuery] bool removeAll, [FromQuery] string accountName, [FromQuery] string vaultName)
        {
            //TODO: Get the account mapping list
            //TODO: If there is no accountName and/or vaultName and removeAll is true, just delete all of the account mappings
            //TODO: If there is an accountName and no vaultName then remove all matching account mappings for accountName
            //TODO: If there is a vaultName and no accountName then remove all matching account mappings for vaultName
            //TODO: If there is an accountName and a vaultName then remove the matching account mapping
            //TODO: Save the new account mapping list to the database
            //TODO:
            var setting = _configurationRepository.GetSetting(WellKnownData.ConfigurationName);
            if (setting == null)
                return NotFound();

            var config = JsonHelper.DeserializeObject<Configuration>(setting.Value);
            return config.AccountMapping.ToArray();
        }

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
