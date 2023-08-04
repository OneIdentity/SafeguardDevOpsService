using System.Collections.Generic;
using OneIdentity.DevOps.Data;

#pragma warning disable 1591

namespace OneIdentity.DevOps.ConfigDb
{
    public interface IPluginRepository
    {
        IEnumerable<Plugin> GetAllPlugins();
        Plugin GetPluginByName(string name);
        IEnumerable<Plugin> GetPluginInstancesByName(string name);
        IEnumerable<Plugin> GetAllReverseFlowPluginInstances();
        Plugin SavePluginConfiguration(Plugin plugin);
        bool DeletePluginByName(string name, bool hardDelete = false);
    }
}
