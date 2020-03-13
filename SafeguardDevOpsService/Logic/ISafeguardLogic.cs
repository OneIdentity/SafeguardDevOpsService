using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    public interface ISafeguardLogic
    {
        SafeguardAvailability GetSafeguardData();
        SafeguardAvailability SetSafeguardData(SafeguardData safeguardData);
        void DeleteSafeguardData();

        SafeguardConnection GetConnection();
        SafeguardConnection Connect(SafeguardConnectionRequest connectionData);
        void Disconnect();
    }
}
