using System;
using System.Collections.Generic;
using System.Text;

namespace OneIdentity.SafeguardDevOpsService.Data
{
    public class AccountMapping
    {
        public string AccountName { get; set; }
        public string VaultName { get; set; }

        public bool Equals(AccountMapping other)
        {
            return (String.Compare(AccountName, other.AccountName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (String.Compare(VaultName, other.VaultName, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
