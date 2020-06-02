using System;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Data
{
    public class LoadedPlugin
    {
        public ILoadablePlugin LoadablePlugin { get; set; }
        public AssemblyLoader AssemblyLoader { get; set; }
    }
}
