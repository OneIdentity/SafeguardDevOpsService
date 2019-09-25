using System;
using System.Collections.Generic;
using System.Text;

namespace OneIdentity.SafeguardDevOpsService.Data
{
    public class InitialConfiguration : ClientCertificate
    {
        public IEnumerable<AccountMapping> AccountMapping { get; set; }
    }
}
