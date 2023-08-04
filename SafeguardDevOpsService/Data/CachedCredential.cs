using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneIdentity.DevOps.Common;

namespace OneIdentity.DevOps.Data
{
    internal class CachedCredential
    {
        public AccountMapping Account { get; set; }
        public CredentialType CredentialType { get; set; }
        public string Credential { get; set; }
    }
}
