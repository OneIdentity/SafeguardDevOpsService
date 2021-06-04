using System;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IDeployAddOn
    {
        void Deploy(AddOnManifest addOnManifest, Tuple<string, string> certs);
        void SetLogger(ILogger logger);
        void SetTempDirectory(string tempDirectory);
    }
}
