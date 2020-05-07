using System.Collections.Generic;
using System.Security;
using System.Security.Cryptography.X509Certificates;
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

        IEnumerable<AccountMapping> GetAccountMappings();
        void SaveAccountMappings(IEnumerable<AccountMapping> accounts);
        void DeleteAccountMappingsByKey(string key);
        void DeleteAccountMappings();


        string SafeguardAddress { get; set; }
        int? ApiVersion { get; set; }
        bool? IgnoreSsl { get; set; }
        int? A2aUserId { get; set; }
        int? A2aRegistrationId { get; set; }
        string SigningCertificate { get; set; }

        string UserCertificateThumbprint { get; set; }
        string UserCertificateBase64Data { get; set; }
        string UserCertificatePassphrase { get; set; }
        string CsrBase64Data { get; set; }
        string CsrPrivateKeyBase64Data { get; set; }

        X509Certificate2 UserCertificate { get; }
    }
}
