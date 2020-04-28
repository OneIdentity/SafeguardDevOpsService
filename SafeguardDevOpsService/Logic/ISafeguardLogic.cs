using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    public interface ISafeguardLogic
    {
        Safeguard GetSafeguardData();
        Safeguard SetSafeguardData(SafeguardData safeguardData);
        void DeleteSafeguardData();

        bool ValidateLogin(string token, bool tokenOnly = false);
        void InstallClientCertificate(ClientCertificatePfx certificatePfx);
        ClientCertificate GetClientCertificate();
        void RemoveClientCertificate();
        string GetClientCSR(int? size, string subjectName);

        ManagementConnection GetConnection();
        ManagementConnection Connect(ManagementConnectionData connectionData);
        void Disconnect();


        // Delete ME - only used for developement
        void CreateA2AUser();

    }
}
