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
        ClientCertificate GetClientCertificate(string thumbPrint);
        void RemoveClientCertificate(string thumbPrint);

        ManagementConnection GetConnection();
        ManagementConnection Connect(ManagementConnectionData connectionData);
        void Disconnect();
    }
}
