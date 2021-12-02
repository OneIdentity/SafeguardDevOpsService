#pragma warning disable 1591

namespace OneIdentity.DevOps.Common
{
    public class AddonManifest
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string SourceFolder { get; set; }
        public string DestinationFolder { get; set; }
        public string Assembly { get; set; }
        public string AssemblyName { get; set; }
        public string ServiceClassName { get; set; }
        public string DeployClassName { get; set; }
        public string UndeployClassName { get; set; }
        public string PluginName { get; set; }
        public bool IsPluginSystemOwned { get; set; }
        public string VaultUrl { get; set; }
        public string ProxyUrl { get; set; }
        public string Version { get; set; }
    }
}
