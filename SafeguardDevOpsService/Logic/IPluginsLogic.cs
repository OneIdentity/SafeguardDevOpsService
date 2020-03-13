using System.Collections.Generic;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    public interface IPluginsLogic
    {
        IEnumerable<Plugin> GetAllPlugins();
        Plugin GetPluginByName(string name);
        void DeletePluginByName(string name);
        Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name);
    }
}
