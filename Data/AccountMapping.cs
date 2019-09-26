using System;
using LiteDB;

namespace OneIdentity.SafeguardDevOpsService.Data
{
    public class AccountMapping
    {
        [BsonId]
        public string ApiKey { get; set; }
        public string AccountName { get; set; }
        public string VaultName { get; set; }

        public bool Equals(AccountMapping other)
        {
            return (String.Compare(AccountName, other.AccountName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (String.Compare(VaultName, other.VaultName, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
