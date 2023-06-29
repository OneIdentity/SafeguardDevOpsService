using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;

#pragma warning disable 1591

namespace OneIdentity.DevOps.ConfigDb
{
    public interface IConfigurationRepository : ISettingsRepository, IPluginRepository, IAddonRepository
    {
        IEnumerable<AccountMapping> GetAccountMappings();
        IEnumerable<AccountMapping> GetAccountMappings(string name);
        void SaveAccountMappings(IEnumerable<AccountMapping> accounts);
        void DeleteAccountMappingsByKey(string key);
        void DeleteAccountMappings();
        Plugin SetRootPlugin(string name, bool isRoot);

        IEnumerable<TrustedCertificate> GetAllTrustedCertificates();
        TrustedCertificate GetTrustedCertificateByThumbPrint(string thumbprint);
        TrustedCertificate SaveTrustedCertificate(TrustedCertificate trustedCertificate);
        void DeleteTrustedCertificateByThumbPrint(string thumbprint);
        void DeleteAllTrustedCertificates();
        X509Chain GetTrustedChain();

        string SafeguardAddress { get; set; }
        int? ApiVersion { get; set; }
        string SvcId { get; set; }
        string DbPasswd { get; }
        bool? IgnoreSsl { get; set; }
        int? A2aUserId { get; set; }
        int? A2aRegistrationId { get; set; }
        int? A2aVaultRegistrationId { get; set; }
        int? AssetId { get; set; }
        int? AssetPartitionId { get; set; }
        int? AssetAccountGroupId { get; set; }
        string SigningCertificate { get; set; }
        string LastKnownMonitorState { get; set; }
        string LastKnownReverseFlowMonitorState { get; set; }
        int? ReverseFlowPollingInterval { get; set; }

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

        Tuple<string, string> GetWebSslPemCertificate();

        DevOpsSecretsBroker DevOpsSecretsBroker { get; }

        void CheckPoint();
        void DropDatabase();
        string SavePassword(string password);
    }
}
