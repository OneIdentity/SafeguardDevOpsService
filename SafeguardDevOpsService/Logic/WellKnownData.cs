using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using OneIdentity.DevOps.Authorization;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public static class WellKnownData
    {
        public const string PluginInfoClassName = "PluginDescriptor";

        public const string DevOpsServiceName = "SafeguardDevOpsService";
        public const string DevOpsUserName = "SafeguardDevOpsUser";

        public const string DevOpsRegistrationName = DevOpsServiceName;
        public const string DevOpsVaultRegistrationName = DevOpsRegistrationName + "VaultCredentials";

        public const string DevOpsServiceClientCertificate = "CN=DevOpsServiceClientCertificate";
        public const string DevOpsServiceWebSslCertificate = "CN=DevOpsServiceWebSslCertificate";

        public const string ManifestPattern = "Manifest.json";
        public const string DllExtension = ".dll";
        public const string DllPattern = "*.dll";

        public const string PluginDirName = "ExternalPlugins";
        public const string PluginStageName = "PluginStaging";
        public const string PluginVaultCredentialName = "VaultCredential";

        public static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), WellKnownData.DevOpsServiceName);
        public static readonly string AppDataPathExt = Path.Combine(@"\", DevOpsServiceName);
        public static readonly string PluginDirPath = Path.Combine(AppDataPathExt, PluginDirName);
        public static readonly string PluginStageDirPath = Path.Combine(AppDataPathExt, PluginDirName, PluginStageName);

        public static string GetSppToken(HttpContext context)
        {
            var authHeader = context.Request.Headers.FirstOrDefault(c => c.Key == "Authorization");
            var sppToken = authHeader.Value.ToString();
            if (!sppToken.StartsWith("spp-token ", StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return sppToken.Split(" ").LastOrDefault();
        }
    }
}
