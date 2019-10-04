
using System.Collections.Generic;
using OneIdentity.Common;
using Serilog;

namespace OneIdentity.SafeguardDevOpsPlugin.TerraForm
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static TerraFormDevOpsPlugin _plugin = null;
        private static ILogger _logger = null;

        public PluginDescriptor()
        {
            _plugin = new TerraFormDevOpsPlugin();                
        }

        public string Name { get; } = "TerraForm";
        public string Description { get; } = "This is the TerraForm plugin for updating the passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            //TODO: Make a call here to get the current configuration of the plugin
            return _plugin.InitializeConfiguration();
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            //TODO: Make a call here to configure the plugin with the new configuration.
            _plugin.Configuration = configuration;
        }

        public bool SetPassword(string account, string password)
        {
            if (_plugin == null)
                return false;

            //TODO: Check if it is configured.

            //TODO: Make a call here to set the new password for the account.
            _plugin.ProcessPassword(account, password);
            return true;
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }
    }
}
