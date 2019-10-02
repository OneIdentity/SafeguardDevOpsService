
using System.Collections.Generic;
using OneIdentity.Common;

namespace OneIdentity.SafeguardDevOpsPlugin.TerraForm
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static TerraFormDevOpsPlugin _plugin = null;

        public PluginDescriptor()
        {
        }

        public string Name { get; } = "TerraForm";
        public string Description { get; } = "This is the TerraForm plugin for updating the passwords";

        public Dictionary<string,string> GetPluginConfiguration()
        {
            //TODO: Make a call here to get the current configuration of the plugin
            return new Dictionary<string, string>();
        }

        public Dictionary<string,string> SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            _plugin = new TerraFormDevOpsPlugin(configuration);

            //TODO: Make a call here to configure the plugin with the new configuration.
            return _plugin.InitializeConfiguration();
        }

        public bool SetPassword(string account, string password)
        {
            if (_plugin == null)
                return false;

            //TODO: Make a call here to set the new password for the account.
            _plugin.ProcessPassword(account, password);
            return true;
        }
    }
}
