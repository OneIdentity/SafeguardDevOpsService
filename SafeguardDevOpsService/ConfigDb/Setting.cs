using LiteDB;

namespace OneIdentity.DevOps.ConfigDb
{
    internal class Setting : ISetting
    {
        [BsonId]
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
