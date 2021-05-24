using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IDeployAddon
    {
        void Deploy();
        void SetLogger(ILogger logger);
        void SetTempDirectory(string tempDirectory);
    }
}
