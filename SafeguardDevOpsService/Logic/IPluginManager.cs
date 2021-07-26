using System.Security;
using OneIdentity.SafeguardDotNet;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IPluginManager
    {
        void Run();
        void SetConfigurationForPlugin(string name);
        void SendPluginVaultCredentials(string plugin, string apiKey);
        bool TestPluginVaultConnection(ISafeguardConnection sgConnection, string plugin);
        bool SendPassword(string name, string assetName, string accountName, SecureString password, string altAccountName = null);
        bool IsLoadedPlugin(string name);
        bool IsDisabledPlugin(string name);

        void RefreshPluginCredentials(ISafeguardConnection sgConnection);
    }
}
