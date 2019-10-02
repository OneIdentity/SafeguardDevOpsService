using System;
using System.Collections.Generic;
using System.Text;

namespace OneIdentity.SafeguardDevOpsService.Plugins
{
    public interface IPluginManager
    {
        void Run();
        void SetConfigurationforPlugin(string name);

    }
}
