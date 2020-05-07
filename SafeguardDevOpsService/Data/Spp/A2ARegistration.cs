using System;

namespace OneIdentity.DevOps.Data.Spp
{
    public class A2ARegistration
    {
        public int Id { get; set; }
        public string AppName { get; set; }
        public string Description { get; set; }
        public bool Disabled { get; set; }
        public bool VisibleToCertificateUsers { get; set; }
        public int CertificateUserId { get; set; }
        public string CertificateUserThumbPrint { get; set; }
        public string CertificateUser { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByUserDisplayName { get; set; }
    }
}
