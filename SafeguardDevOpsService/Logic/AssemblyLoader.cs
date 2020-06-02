using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace OneIdentity.DevOps.Logic
{
    public class AssemblyLoader : AssemblyLoadContext
    {
        
        private string _externalPath;

        public AssemblyLoader(string externalPath) : base(isCollectible: true)
//        public AssemblyLoader() : base(isCollectible: true)
        {
            _externalPath = externalPath;
        }

        protected override Assembly Load(AssemblyName name)
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(name);
                if (assembly != null)
                    return assembly;
            }
            catch {}

            try
            {
                string assemblyPath = Path.Join(_externalPath, name.Name);
                if (assemblyPath != null)
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }
            }
            catch {}

            return null;
        }
    }
}
