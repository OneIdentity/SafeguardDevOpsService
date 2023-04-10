using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface ISafeguardLogic
    {
        DevOpsSecretsBroker DevOpsSecretsBrokerCache { get; }

        ISafeguardConnection Connect();
        ISafeguardConnection CertConnect();
        SafeguardDevOpsConnection GetAnonymousSafeguardConnection();
        SafeguardDevOpsConnection GetSafeguardConnection();
        SafeguardDevOpsLogon GetSafeguardLogon();
        SafeguardDevOpsConnection SetSafeguardData(string token, SafeguardData safeguardData);
        bool SetThreadData(string token);

        bool ValidateLicense();
        bool ValidateLogin(string token, bool tokenOnly = false);
        bool PauseBackgroundMaintenance { get; }

        CertificateInfo GetCertificateInfo(CertificateType certificateType);
        void InstallCertificate(CertificateInfo certificatePfx, CertificateType certificateType);
        void RemoveClientCertificate();
        void RemoveWebServerCertificate();
        string GetCSR(int? size, string subjectName, string sanDns, string sanIp, CertificateType certificateType);

        object GetAvailableAccounts(ISafeguardConnection sgConnection, string filter, int? page, bool? count, int? limit, string orderby, string q);
        IEnumerable<AssetAccount> GetAssetAccounts(ISafeguardConnection sgConnection, int assetId);
        AssetAccount GetAssetAccount(ISafeguardConnection sgConnection, int accountId);
        AssetAccount AddAssetAccount(ISafeguardConnection sgConnection, AssetAccount account);
        bool DeleteAssetAccounts(ISafeguardConnection sgConnection, int assetId);
        void SetAssetAccountPassword(ISafeguardConnection sgConnection, AssetAccount account, string password);
        Asset CreateAsset(ISafeguardConnection sgConnection, AssetPartition assetPartition);
        AssetPartition CreateAssetPartition(ISafeguardConnection sgConnection);
        AssetAccountGroup CreateAssetAccountGroup(ISafeguardConnection sgConnection, Addon addon);

        public Asset GetAsset(ISafeguardConnection sgConnection);

        object GetAvailableA2ARegistrations(ISafeguardConnection sgConnection, string filter, int? page, bool? count, int? limit, string @orderby, string q);
        A2ARegistration GetA2ARegistration(ISafeguardConnection sgConnection, A2ARegistrationType registrationType);
        A2ARegistration SetA2ARegistration(ISafeguardConnection sgConnection, int id);
        A2ARetrievableAccount GetA2ARetrievableAccount(ISafeguardConnection sgConnection, int id, A2ARegistrationType registrationType);
        void DeleteA2ARetrievableAccount(ISafeguardConnection sgConnection, int id, A2ARegistrationType registrationType);
        IEnumerable<A2ARetrievableAccount> GetA2ARetrievableAccounts(ISafeguardConnection sgConnection, A2ARegistrationType registrationType);
        A2ARetrievableAccount GetA2ARetrievableAccountById(ISafeguardConnection sgConnection, A2ARegistrationType registrationType, int accountId);
        IEnumerable<A2ARetrievableAccount> AddA2ARetrievableAccounts(ISafeguardConnection sgConnection, IEnumerable<SppAccount> accounts, A2ARegistrationType registrationType);
        void RemoveA2ARetrievableAccounts(ISafeguardConnection sgConnection, IEnumerable<A2ARetrievableAccount> accounts, A2ARegistrationType registrationType);

        void RetrieveDevOpsSecretsBrokerInstance(ISafeguardConnection sgConnection);
        void AddSecretsBrokerInstance(ISafeguardConnection sgConnection);
        void CheckAndSyncSecretsBrokerInstance(ISafeguardConnection sgConnection);
        void CheckAndPushAddOnCredentials(ISafeguardConnection sgConnection);
        void CheckAndConfigureAddonPlugins(ISafeguardConnection sgConnection);
        void CheckAndSyncVaultCredentials(ISafeguardConnection sgConnection);

        DevOpsSecretsBroker GetDevOpsConfiguration(ISafeguardConnection sgConnection);
        DevOpsSecretsBroker ConfigureDevOpsService();
        void DeleteDevOpsConfiguration(ISafeguardConnection sgConnection, bool secretsBrokerOnly);

        string BackupDevOpsConfiguration(string bkPassphrase);
        void RestoreDevOpsConfiguration(IFormFile formFile, string bkPassphrase);
        void RestoreDevOpsConfiguration(string base64Backup, string passphrase);

        void RestartService();

        IEnumerable<CertificateInfo> GetTrustedCertificates();
        CertificateInfo GetTrustedCertificate(string thumbPrint);
        CertificateInfo AddTrustedCertificate(CertificateInfo certificate);
        void DeleteTrustedCertificate(string thumbPrint);
        IEnumerable<CertificateInfo> ImportTrustedCertificates(ISafeguardConnection sgConnection);
        void DeleteAllTrustedCertificates();
        void PingSpp(ISafeguardConnection sgConnection);
    }
}
