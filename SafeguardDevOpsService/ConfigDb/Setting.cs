using LiteDB;

namespace OneIdentity.DevOps.ConfigDb
{
    public class Setting
    {
        [BsonId]
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
