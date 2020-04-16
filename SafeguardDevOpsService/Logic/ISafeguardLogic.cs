using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    public interface ISafeguardLogic
    {
        Safeguard GetSafeguardData();
        Safeguard SetSafeguardData(SafeguardData safeguardData);
        void DeleteSafeguardData();

        bool ValidateToken(string token);

        ManagementConnection GetConnection();
        ManagementConnection Connect(ManagementConnectionData connectionData);
        void Disconnect();
    }
}
