using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using Serilog.Core;

namespace OneIdentity.DevOps.Common
{
    public interface ILoadablePlugin
    {
        string Name { get; }
        string DisplayName { get; }
        string Description { get; }
        CredentialType[] SupportedCredentialTypes { get; }
        bool SupportsReverseFlow { get; }

        CredentialType AssignedCredentialType { get; set; }
        bool ReverseFlowEnabled { get; set; }
        ILogger Logger { get; set; }

        Dictionary<string, string> GetPluginInitialConfiguration();
        void SetPluginConfiguration(Dictionary<string, string> configuration);
        void SetVaultCredential(string credential);
        string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName);
        string SetCredential(CredentialType credentialType, string asset, string account, string[] credential, string altAccountName);
        bool TestVaultConnection();
        void Unload();
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CredentialType
    {
        Password,
        SshKey,
        ApiKey,
        Unknown
    }
}
