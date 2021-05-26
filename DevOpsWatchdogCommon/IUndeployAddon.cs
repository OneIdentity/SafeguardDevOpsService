using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IUndeployAddon
    {
        void Undeploy();
        void SetLogger(ILogger logger);
        void SetPluginDb(IPluginRepository pluginDb);
    }
}
