
using Newtonsoft.Json;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Retrievable account
    /// </summary>
    public class A2ARetrievableAccount
    {
        /// <summary>
        /// Retrievable account
        /// </summary>
        public int AccountId { get; set; }
        /// <summary>
        /// Account name
        /// </summary>
        public string AccountName { get; set; }
        /// <summary>
        /// Account description
        /// </summary>
        public string AccountDescription { get; set; }
        /// <summary>
        /// Is account disabled
        /// </summary>
        public bool AccountDisabled { get; set; }
        /// <summary>
        /// Account type
        /// </summary>
        public string AccountType { get; set; }
        /// <summary>
        /// Api key
        /// </summary>
        public string ApiKey { get; set; }
        /// <summary>
        /// Asset Id
        /// </summary>
        public int SystemId { get; set; }
        /// <summary>
        /// Asset name
        /// </summary>
        public string SystemName { get; set; }
        /// <summary>
        /// Asset description
        /// </summary>
        public string SystemDescription { get; set; }
        /// <summary>
        /// Asset network address
        /// </summary>
        public string NetworkAddress { get; set; }
        /// <summary>
        /// Asset domain name
        /// </summary>
        public string DomainName { get; set; }
        /// <summary>
        /// IP address restrictions
        /// </summary>
        public string[] IpRestrictions { get; set; }
    }
}
