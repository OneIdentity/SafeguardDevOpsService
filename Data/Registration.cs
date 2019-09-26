using System;
using System.Collections.Generic;
using System.Text;
using LiteDB;

namespace OneIdentity.SafeguardDevOpsService.Data
{
    public class Registration
    {
        public string SpsAddress { get; set; }
        public int A2ARegistrationId { get; set; }
        public string A2ARegistrationName { get; set; }
        public string CertificateUserThumbPrint { get; set; }
        public string CertificateUser { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByUserDisplayName { get; set; }
    }
}
