using OneIdentity.Common;
using System;
using System.Reflection;

namespace TestPlugins
{
    class Program
    {
        static void Main(string[] args)
        {
            string pluginPath = @"C:\1Identity\Advanced Services\Safeguard\Projects\DevOpsService\DevOpsService\ExternalPlugins\SafeguardDevOpsPluginTerraForm\bin\Debug\netcoreapp2.2\OneIdentity.SafeguardDevOpsPlugin.TerraForm.dll";
            var assembly = Assembly.LoadFrom(pluginPath);

            Type[] types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.IsSubclassOf(typeof(ILoadablePlugin)))
                {
                    var pluginBase = (ILoadablePlugin)Activator.CreateInstance(type);
                    var name = pluginBase.Name;
                    var description = pluginBase.Description;

                }
            }

        }
    }
}
