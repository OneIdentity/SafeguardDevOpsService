using System.Collections.Generic;

namespace OneIdentity.Common
{
    public interface ILoadablePlugin
    {
        string Name { get; }
        string Description { get; }
        Dictionary<string, string> GetPluginConfiguration();
        Dictionary<string, string> SetPluginConfiguration(Dictionary<string, string> configuration);
        bool SetPassword(string account, string password);
    }
}
