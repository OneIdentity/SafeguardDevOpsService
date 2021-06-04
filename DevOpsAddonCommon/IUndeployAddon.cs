using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IUndeployAddon
    {
        void Undeploy(AddonManifest addonManifest);
        void SetLogger(ILogger logger);
//        void SetConfigDb(object configDb);
    }
}
