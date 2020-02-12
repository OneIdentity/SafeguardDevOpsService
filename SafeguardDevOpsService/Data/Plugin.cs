
using LiteDB;

namespace OneIdentity.DevOps.Data
{
    public class Plugin : PluginConfiguration
    {
        [BsonId]
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
