using System.Collections.Generic;
using System.Security;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;

namespace OneIdentity.DevOps.Logic
{
    public interface ISafeguardLogic
    {
        ISafeguardConnection Connect();
        SafeguardConnection GetSafeguardConnection();
        SafeguardConnection SetSafeguardData(string token, SafeguardData safeguardData);

        bool ValidateLogin(string token, bool tokenOnly = false);

        CertificateInfo GetCertificateInfo(CertificateType certificateType);
        void InstallCertificate(CertificateInfo certificatePfx, CertificateType certificateType);
        void RemoveClientCertificate();
        void RemoveWebServerCertificate();
        string GetCSR(int? size, string subjectName, CertificateType certificateType);

        IEnumerable<SppAccount> GetAvailableAccounts();

        A2ARegistration GetA2ARegistration();
        void DeleteA2ARegistration();
        IEnumerable<A2ARetrievableAccount> GetA2ARetrievableAccounts();
        IEnumerable<A2ARetrievableAccount> AddA2ARetrievableAccounts(IEnumerable<SppAccount> accounts);

        ServiceConfiguration GetDevOpsConfiguration();
        ServiceConfiguration ConfigureDevOpsService();
        void DeleteDevOpsConfiguration();

        void RestartService();

    }
}
