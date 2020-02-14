using System.Security;

namespace OneIdentity.DevOps.Plugins
{
    public interface IPluginManager
    {
        void Run();
        void SetConfigurationForPlugin(string name);
        bool SendPassword(string name, string accountName, SecureString password);
    }
}
