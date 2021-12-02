using LiteDB;
using OneIdentity.DevOps.ConfigDb;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Represents a setting, i.e. a key-value pair of configuration information.
    /// </summary>
    public class Setting : ISetting
    {
        /// <summary>
        /// The name (or key) of the setting.
        /// </summary>
        [BsonId]
        public string Name { get; set; }
        /// <summary>
        /// The value of the setting.
        /// </summary>
        public string Value { get; set; }
    }
}
