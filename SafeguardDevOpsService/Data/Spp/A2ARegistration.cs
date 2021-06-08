using System;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// A2A registration
    /// </summary>
    public class A2ARegistration
    {
        /// <summary>
        /// Registration Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Application name
        /// </summary>
        public string AppName { get; set; }
        /// <summary>
        /// Registration description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// DevOps instance id
        /// </summary>
        public string DevOpsInstanceId { get; set; }
        /// <summary>
        /// Is the registration disabled
        /// </summary>
        public bool Disabled { get; set; }
        /// <summary>
        /// Is the certificate user visible
        /// </summary>
        public bool VisibleToCertificateUsers { get; set; }
        /// <summary>
        /// Certificate user Id
        /// </summary>
        public int CertificateUserId { get; set; }
        /// <summary>
        /// Certificate user thumb print
        /// </summary>
        public string CertificateUserThumbPrint { get; set; }
        /// <summary>
        /// Certificate user
        /// </summary>
        public string CertificateUser { get; set; }
        /// <summary>
        /// Create date
        /// </summary>
        public DateTime CreatedDate { get; set; }
        /// <summary>
        /// Created by user Id
        /// </summary>
        public int CreatedByUserId { get; set; }
        /// <summary>
        /// Created by user display name
        /// </summary>
        public string CreatedByUserDisplayName { get; set; }
    }
}
