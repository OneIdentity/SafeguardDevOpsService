using System.Collections.Generic;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;
using Safeguard = OneIdentity.DevOps.Data.Safeguard;

namespace OneIdentity.DevOps.Logic
{
    public interface ISafeguardLogic
    {
        ISafeguardConnection Connect();
        Safeguard GetSafeguardConnection();
        Safeguard SetSafeguardData(SafeguardData safeguardData);

        bool ValidateLogin(string token, bool tokenOnly = false);
        void InstallClientCertificate(ClientCertificate certificatePfx);
        ClientCertificate GetClientCertificate();
        void RemoveClientCertificate();
        string GetClientCSR(int? size, string subjectName);

        IEnumerable<SppAccount> GetAvailableAccounts();

        A2ARegistration GetA2ARegistration();
        void DeleteA2ARegistration();
        IEnumerable<A2ARetrievableAccount> GetA2ARetrievableAccounts();
        IEnumerable<A2ARetrievableAccount> AddA2ARetrievableAccounts(IEnumerable<SppAccount> accounts);

        ManagementConnection Connect(ManagementConnectionData connectionData);
        void Disconnect();

        ManagementConnection GetDevOpsConfiguration();
        ManagementConnection ConfigureDevOpsService();
        void DeleteDevOpsConfiguration();

    }
}
