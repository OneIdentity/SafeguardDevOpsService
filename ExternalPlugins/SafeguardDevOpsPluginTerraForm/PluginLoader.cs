
using OneIdentity.Common;

namespace OneIdentity.SafeguardDevOpsPlugin.TerraForm
{
    public class PluginDescriptor : ILoadablePlugin
    {
        public PluginDescriptor()
        {
        }

        public string Name { get; } = "TerraForm";
        public void GetPluginInfo()
        {
        }
    }
}
