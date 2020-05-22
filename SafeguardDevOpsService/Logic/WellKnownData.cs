using System;
using System.IO;
using Microsoft.AspNetCore.SignalR;

namespace OneIdentity.DevOps.Logic
{
    public static class WellKnownData
    {
        public const string PluginInfoClassName = "PluginDescriptor";

        public const string DevOpsServiceName = "SafeguardDevOpsService";
        public const string DevOpsUserName = "SafeguardDevOpsUser";

        public const string DevOpsServiceClientCertificate = "CN=DevOpsServiceClientCertificate";
        public const string DevOpsServiceWebSslCertificate = "CN=DevOpsServiceWebSslCertificate";

        public const string DllExtension = ".dll";
        public const string DllPattern = "*.dll";

        public static string AppDataPath
        {
            get
            {
                var dirPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(dirPath, WellKnownData.DevOpsServiceName);
            }
        }
    }
}
