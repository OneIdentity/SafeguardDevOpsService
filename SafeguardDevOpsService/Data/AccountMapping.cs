using System;
using LiteDB;

namespace OneIdentity.DevOps.Data
{
    public class AccountMapping
    {
        [BsonId]
        public string ApiKey { get; set; }
        public string AccountName { get; set; }
        public string VaultName { get; set; }

        public bool Equals(AccountMapping other)
        {
            return (string.Compare(AccountName, other.AccountName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (string.Compare(VaultName, other.VaultName, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
