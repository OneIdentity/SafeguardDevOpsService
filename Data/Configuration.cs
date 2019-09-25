using System.Collections.Generic;

namespace OneIdentity.SafeguardDevOpsService.Data
{
    public class Configuration : Registration
    {
        public IEnumerable<AccountMapping> AccountMapping { get; set; }
    }
}
