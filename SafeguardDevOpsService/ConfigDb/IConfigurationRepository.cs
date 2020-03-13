using System.Collections.Generic;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.ConfigDb
{
    public interface IConfigurationRepository
    {
        ISetting GetSetting(string name);
        void SetSetting(ISetting value);
        void RemoveSetting(string name);

        IEnumerable<Plugin> GetAllPlugins();
        Plugin GetPluginByName(string name);
        Plugin SavePluginConfiguration(Plugin plugin);
        void DeletePluginByName(string name);


        string SafeguardAddress { get; set; }
        int? ApiVersion { get; set; }
        bool? IgnoreSsl { get; set; }
    }
}
