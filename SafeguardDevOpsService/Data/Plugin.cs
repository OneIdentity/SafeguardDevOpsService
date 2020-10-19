
using LiteDB;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Represents a DevOps plugin
    /// </summary>
    public class Plugin : PluginConfiguration
    {
        /// <summary>
        /// Name of the plugin
        /// </summary>
        [BsonId]
        public string Name { get; set; }

        /// <summary>
        /// Display name of the plugin
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Description of the plugin
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Base64 representation of the plugin (write-only)
        /// </summary>
        public string Base64PluginData { get; set; }

        /// <summary>
        /// A2A registration vault account id
        /// </summary>
        public int? VaultAccountId { get; set; }

        /// <summary>
        /// Is the plugin loaded
        /// </summary>
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Plugin version
        /// </summary>
        public string Version { get; set; }
    }
}
