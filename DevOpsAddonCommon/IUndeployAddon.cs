using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IUndeployAddOn
    {
        void Undeploy(AddOnManifest addOnManifest);
        void SetLogger(ILogger logger);
//        void SetConfigDb(object configDb);
    }
}
