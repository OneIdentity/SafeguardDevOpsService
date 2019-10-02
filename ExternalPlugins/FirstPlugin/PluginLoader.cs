
using OneIdentity.Common;

namespace FirstPlugin
{
    public class PluginLoader : ILoadablePlugin
    {
        public PluginLoader()
        {
        }

        public string Name { get; } = "First Plugin";
        public void GetPluginInfo()
        {
            int x = 1;
        }
    }
}
