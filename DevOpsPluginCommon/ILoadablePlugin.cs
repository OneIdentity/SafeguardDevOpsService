using System.Collections.Generic;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface ILoadablePlugin
    {
        string Name { get; }
        string DisplayName { get; }
        string Description { get; }
        CredentialType[] SupportedCredentialTypes { get; }
        CredentialType AssignedCredentialType { get; set; }
        Dictionary<string, string> GetPluginInitialConfiguration();
        void SetPluginConfiguration(Dictionary<string, string> configuration);
        void SetVaultCredential(string credential);
        bool SetPassword(string asset, string account, string password, string altAccountName = null);
        bool SetSshKey(string asset, string account, string sshKey, string altAccountName = null);
        bool SetApiKey(string asset, string account, string clientId, string clientSecret, string altAccountName = null);
        void SetLogger(ILogger logger);
        bool TestVaultConnection();
        void Unload();
    }

    public enum CredentialType
    {
        Password,
        SshKey,
        ApiKey,
        Unknown
    }
}
