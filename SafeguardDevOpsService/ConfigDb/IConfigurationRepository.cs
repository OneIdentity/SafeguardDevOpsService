using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using OneIdentity.DevOps.Data;
#pragma warning disable 1591

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

        IEnumerable<TrustedCertificate> GetAllTrustedCertificates();
        TrustedCertificate GetTrustedCertificateByThumbPrint(string thumbprint);
        TrustedCertificate SaveTrustedCertificate(TrustedCertificate trustedCertificate);
        void DeleteTrustedCertificateByThumbPrint(string thumbprint);
        void DeleteAllTrustedCertificates();
        X509Chain GetTrustedChain();

        string SafeguardAddress { get; set; }
        int? ApiVersion { get; set; }
        string SvcId { get; }
        bool? IgnoreSsl { get; set; }
        bool? PendingRemoval { get; set; }
        int? A2aUserId { get; set; }
        int? A2aRegistrationId { get; set; }
        int? A2aVaultRegistrationId { get; set; }
        string SigningCertificate { get; set; }

        string UserCertificateThumbprint { get; set; }
        string UserCertificateBase64Data { get; set; }
        string UserCertificatePassphrase { get; set; }
        string UserCsrBase64Data { get; set; }
        string UserCsrPrivateKeyBase64Data { get; set; }
        string WebSslCertificateBase64Data { get; set; }
        string WebSslCertificatePassphrase { get; set; }
        string WebSslCsrBase64Data { get; set; }
        string WebSslCsrPrivateKeyBase64Data { get; set; }

        X509Certificate2 UserCertificate { get; }
        X509Certificate2 WebSslCertificate { get; set; }

        void DropDatabase();
    }
}
