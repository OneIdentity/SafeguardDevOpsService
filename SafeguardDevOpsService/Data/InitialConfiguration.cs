using System.Collections.Generic;

namespace OneIdentity.DevOps.Data
{
    public class InitialConfiguration : ConnectionConfiguration
    {
        public IEnumerable<AccountMapping> AccountMapping { get; set; }
    }
}
