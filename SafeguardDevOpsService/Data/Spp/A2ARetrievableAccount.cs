
using Newtonsoft.Json;

namespace OneIdentity.DevOps.Data.Spp
{
    public class A2ARetrievableAccount
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; }
        public string AccountDescription { get; set; }
        public bool AccountDisabled { get; set; }
        public string AccountType { get; set; }
        public string ApiKey { get; set; }
        public int SystemId { get; set; }
        public string SystemName { get; set; }
        public string SystemDescription { get; set; }
        public string NetworkAddress { get; set; }
        public string DomainName { get; set; }
        public string[] IpRestrictions { get; set; }
    }
}
