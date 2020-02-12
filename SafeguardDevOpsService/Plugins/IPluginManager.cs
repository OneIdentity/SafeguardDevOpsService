using System.Security;

namespace OneIdentity.DevOps.Plugins
{
    public interface IPluginManager
    {
        void Run();
        void SetConfigurationforPlugin(string name);
        bool SendPassword(string name, string accountName, SecureString password);
    }
}
