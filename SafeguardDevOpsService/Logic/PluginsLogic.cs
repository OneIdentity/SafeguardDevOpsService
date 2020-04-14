using System.Collections.Generic;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    internal class PluginsLogic : IPluginsLogic
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private readonly IPluginManager _pluginManager;

        public PluginsLogic(IConfigurationRepository configDb, IPluginManager pluginManager)
        {
            _configDb = configDb;
            _pluginManager = pluginManager;
            _logger = Serilog.Log.Logger;
        }

        public IEnumerable<Plugin> GetAllPlugins()
        {
            return _configDb.GetAllPlugins();
        }

        public Plugin GetPluginByName(string name)
        {
            return _configDb.GetPluginByName(name);
        }

        public void DeletePluginByName(string name)
        {
            _configDb.DeletePluginByName(name);
        }


        public Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name)
        {
            var plugin = _configDb.GetPluginByName(name);

            if (plugin == null)
            {
                _logger.Error($"Failed to save the safeguardConnection. No plugin {name} was found.");
                return null;
            }

            plugin.Configuration = pluginConfiguration.Configuration;
            plugin = _configDb.SavePluginConfiguration(plugin);
            _pluginManager.SetConfigurationForPlugin(name);

            return plugin;
        }
    }
}
