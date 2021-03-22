using System.Security;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IPluginManager
    {
        void Run();
        void SetConfigurationForPlugin(string name);
        void SendPluginVaultCredentials(string plugin, string apiKey);
        bool TestPluginVaultConnection(string plugin);
        bool SendPassword(string name, string assetName, string accountName, SecureString password);
        bool IsLoadedPlugin(string name);
        bool IsDisabledPlugin(string name);

        void RefreshPluginCredentials();
    }
}
