using System.Collections.Generic;
using OneIdentity.SafeguardDevOpsService.Data;

namespace OneIdentity.SafeguardDevOpsService.ConfigDb
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

        string SafeguardAddress { get; set; }
        string ClientCertificateThumbprint { get; set; }
        int? ApiVersion { get; set; }
        bool? IgnoreSsl { get; set; }
    }
}
