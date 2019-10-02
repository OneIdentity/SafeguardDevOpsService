using LiteDB;

namespace OneIdentity.SafeguardDevOpsService.ConfigDb
{
    public class Setting
    {
        [BsonId]
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
