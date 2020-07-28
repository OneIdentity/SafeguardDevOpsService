using System.Collections.Generic;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface ISafeguardLogic
    {
        ISafeguardConnection Connect();
        SafeguardConnection GetSafeguardConnection();
        SafeguardConnection SetSafeguardData(string token, SafeguardData safeguardData);
        void DeleteSafeguardData();

        bool IsLoggedIn();
        bool ValidateLogin(string token, bool tokenOnly = false);

        CertificateInfo GetCertificateInfo(CertificateType certificateType);
        void InstallCertificate(CertificateInfo certificatePfx, CertificateType certificateType);
        void RemoveClientCertificate();
        void RemoveWebServerCertificate();
        string GetCSR(int? size, string subjectName, string sanDns, string sanIp, CertificateType certificateType);

        IEnumerable<SppAccount> GetAvailableAccounts(string filter, int? page, int? limit, string orderby, string q);
        AssetAccount GetAccount(int id);

        A2ARegistration GetA2ARegistration(A2ARegistrationType registrationType);
        // void DeleteA2ARegistration();
        A2ARetrievableAccount GetA2ARetrievableAccount(int id, A2ARegistrationType registrationType);
        void DeleteA2ARetrievableAccount(int id, A2ARegistrationType registrationType);
        IEnumerable<A2ARetrievableAccount> GetA2ARetrievableAccounts(A2ARegistrationType registrationType);
        IEnumerable<A2ARetrievableAccount> AddA2ARetrievableAccounts(IEnumerable<SppAccount> accounts, A2ARegistrationType registrationType);
        void RemoveA2ARetrievableAccounts(IEnumerable<A2ARetrievableAccount> accounts, A2ARegistrationType registrationType);

        ServiceConfiguration GetDevOpsConfiguration();
        ServiceConfiguration ConfigureDevOpsService();
        void DeleteDevOpsConfiguration();

        void RestartService();

        IEnumerable<CertificateInfo> GetTrustedCertificates();
        CertificateInfo GetTrustedCertificate(string thumbPrint);
        CertificateInfo AddTrustedCertificate(CertificateInfo certificate);
        void DeleteTrustedCertificate(string thumbPrint);
        IEnumerable<CertificateInfo> ImportTrustedCertificates();
        void DeleteAllTrustedCertificates();
    }
}
