using LiteDB;

namespace OneIdentity.DevOps.Common
{
    public class Setting : ISetting
    {
        [BsonId]
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
