using System.Collections.Generic;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Common
{
    public interface IPluginRepository
    {
        IEnumerable<Plugin> GetAllPlugins();
        Plugin GetPluginByName(string name);
        Plugin SavePluginConfiguration(Plugin plugin);
        void DeletePluginByName(string name);
    }
}
