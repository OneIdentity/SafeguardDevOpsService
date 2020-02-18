using System.Collections.Generic;
using LiteDB;
using Newtonsoft.Json;

namespace OneIdentity.DevOps.Data
{
    public class Configuration : Registration
    {
        [JsonIgnore]
        [BsonId]
        public int Id { get; set; } = 1;
        public IEnumerable<AccountMapping> AccountMapping { get; set; }
    }
}
