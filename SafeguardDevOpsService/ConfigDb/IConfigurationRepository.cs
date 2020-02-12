using System.Collections.Generic;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.ConfigDb
{
    public interface IConfigurationRepository
    {
        IEnumerable<Setting> GetAllSettings();
        Setting GetSetting(string name);
        void SetSetting(Setting value);
        void RemoveSetting(string name);

        Configuration GetConfiguration();
        void SaveConfiguration(Configuration configuration);
        void DeleteConfiguration();

        IEnumerable<Plugin> GetAllPlugins();
        Plugin GetPluginByName(string name);
        Plugin SavePluginConfiguration(Plugin plugin);
        void DeletePluginByName(string name);


        string SafeguardAddress { get; set; }
        string ClientCertificateThumbprint { get; set; }
        int? ApiVersion { get; set; }
        bool? IgnoreSsl { get; set; }
    }
}
