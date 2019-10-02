using System;
using System.Collections.Generic;
using System.Security;
using System.Text;

namespace OneIdentity.SafeguardDevOpsService.Plugins
{
    public interface IPluginManager
    {
        void Run();
        void SetConfigurationforPlugin(string name);
        bool SendPassword(string name, string accountName, SecureString password);
    }
}
