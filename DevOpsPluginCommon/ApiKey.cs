
using Newtonsoft.Json;

namespace OneIdentity.DevOps.Common
{
    public class ApiKey
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string ClientSecretId { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }
}
