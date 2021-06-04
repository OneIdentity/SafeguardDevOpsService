using LiteDB;
using OneIdentity.DevOps.ConfigDb;

namespace OneIdentity.DevOps.Data
{
    public class Setting : ISetting
    {
        [BsonId]
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
