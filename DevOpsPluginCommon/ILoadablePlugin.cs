using System.Collections.Generic;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface ILoadablePlugin
    {
        string Name { get; }
        string DisplayName { get; }
        string Description { get; }
        Dictionary<string, string> GetPluginInitialConfiguration();
        void SetPluginConfiguration(Dictionary<string, string> configuration);
        void SetVaultCredential(string credential);
        bool SetPassword(string asset, string account, string password);
        void SetLogger(ILogger logger);
        void Unload();
    }
}
