using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace OneIdentity.DevOps.Logic
{
    internal static class WellKnownData
    {
        public const string AppSettings = "appsettings";

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

        public static readonly string ProgramDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), DevOpsServiceName);
        public static readonly string ServiceDirPath = Path.GetDirectoryName(
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ?
                Assembly.GetExecutingAssembly().Location : Process.GetCurrentProcess().MainModule.FileName);
        public static readonly string PluginDirPath = Path.Combine(ProgramDataPath, PluginDirName);
        public static readonly string PluginStageDirPath = Path.Combine(ProgramDataPath, PluginDirName, PluginStageName);

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
