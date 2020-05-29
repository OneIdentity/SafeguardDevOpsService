
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
        /// Description of the plugin
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Base64 representation of the plugin (write-only)
        /// </summary>
        public string Base64PluginData { get; set; }

    }
}
