using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using OneIdentity.DevOps.Authorization;

namespace OneIdentity.DevOps.Logic
{
    public static class WellKnownData
    {
        public const string PluginInfoClassName = "PluginDescriptor";

        public const string DevOpsServiceName = "SafeguardDevOpsService";
        public const string DevOpsUserName = "SafeguardDevOpsUser";

        public const string DevOpsServiceClientCertificate = "CN=DevOpsServiceClientCertificate";
        public const string DevOpsServiceWebSslCertificate = "CN=DevOpsServiceWebSslCertificate";

        public const string ManifestPattern = "Manifest.json";


        public const string PluginDirName = "ExternalPlugins";

        public static string AppDataPath
        {
            get
            {
                var dirPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(dirPath, WellKnownData.DevOpsServiceName);
            }
        }

        public static string PluginDirPath
        {
            get
            {
                return Path.Combine(WellKnownData.AppDataPath, WellKnownData.PluginDirName);
            }
        }

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
