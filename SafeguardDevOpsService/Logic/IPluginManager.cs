using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.Data;
using OneIdentity.SafeguardDotNet;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IPluginManager
    {
        void Run();
        void SetConfigurationForPlugin(string name);
        bool TestPluginVaultConnection(ISafeguardConnection sgConnection, string plugin);

        bool SendCredential(AccountMapping account, string[] credential, CredentialType assignedCredentialType);
        string GetCredential(AccountMapping account, CredentialType assignedCredentialType);

        string[] GetAccountCredential(string name, string a2AApiKey, CredentialType assignedType);

        bool IsLoadedPlugin(string name);

        Plugin DuplicatePlugin(string name, bool copyConfig);

        void RefreshPluginCredentials();
    }
}
