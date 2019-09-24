using System.Collections.Generic;

namespace SafeguardDevOpsService.ConfigDb
{
    public interface IConfigurationRepository
    {
        IEnumerable<Setting> GetAllSettings();
        Setting GetSetting(string name);
        void SetSetting(Setting value);
        string SafeguardAddress { get; set; }
        string ClientCertificateThumbprint { get; set; }
        int? ApiVersion { get; set; }
        bool? IgnoreSsl { get; set; }
    }
}
