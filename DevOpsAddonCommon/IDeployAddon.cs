using System;
using System.Security.Cryptography.X509Certificates;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IDeployAddon
    {
        void Deploy(AddonManifest addonManifest, Tuple<string, string> certs);
        void SetLogger(ILogger logger);
        void SetTempDirectory(string tempDirectory);
    }
}
