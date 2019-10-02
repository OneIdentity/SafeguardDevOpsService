using System.Collections.Generic;

namespace OneIdentity.SafeguardDevOpsService.Data
{
    public class InitialConfiguration : ConnectionConfiguration
    {
        public IEnumerable<AccountMapping> AccountMapping { get; set; }
    }
}
