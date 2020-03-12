
using Newtonsoft.Json;

namespace OneIdentity.DevOps.Data.Spp
{
    public class RetrievableAccount
    {
        public string ApiKey { get; set; }
        public string CertificateUserThumbPrint { get; set; }
        // public IpRestrictions (array)
        public int SystemId { get; set; }
        public string SystemName { get; set; }
        public string SystemDescription { get; set; }
        [JsonIgnore]
        public int AssetId => SystemId;
        [JsonIgnore]
        public string AssetName => SystemName;
        [JsonIgnore]
        public string AssetDescription => SystemDescription;
        public string NetworkAddress { get; set; }
        public int AccountId { get; set; }
        public string AccountName { get; set; }
        public string DomainName { get; set; }
    }
}
