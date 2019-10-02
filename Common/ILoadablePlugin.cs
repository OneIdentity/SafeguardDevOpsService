using System;
using System.Collections.Generic;
using System.Text;

namespace OneIdentity.Common
{
    public interface ILoadablePlugin
    {
        string Name { get; }
        void GetPluginInfo();
    }
}
