using System.Security;

namespace OneIdentity.DevOps.Logic
{
    public interface IPluginManager
    {
        void Run();
        void SetConfigurationForPlugin(string name);
        bool SendPassword(string name, string accountName, SecureString password);
        bool IsLoadedPlugin(string name);
    }
}
