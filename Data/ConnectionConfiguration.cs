using System;
using System.Collections.Generic;
using System.Text;

namespace OneIdentity.SafeguardDevOpsService.Data
{
    public class ConnectionConfiguration
    {
        public string SpsAddress { get; set; }
        public string CertificateUserThumbprint { get; set; }
    }
}
