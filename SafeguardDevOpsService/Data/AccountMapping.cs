using System;
using LiteDB;

namespace OneIdentity.DevOps.Data
{
    public class AccountMapping
    {
        [BsonId]
        public string ApiKey { get; set; }
        public string AssetName { get; set; }
        public string NetworkAddress { get; set; }
        public string AccountName { get; set; }
        public string DomainName { get; set; }
        public string VaultName { get; set; }

        public bool Equals(AccountMapping other)
        {
            return (string.Compare(AssetName, other.AssetName, StringComparison.OrdinalIgnoreCase) == 0) && 
                   (string.Compare(AccountName, other.AccountName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (string.Compare(DomainName, other.DomainName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (string.Compare(VaultName, other.VaultName, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
