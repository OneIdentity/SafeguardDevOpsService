using System;
using System.ComponentModel;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Represents a certificate from a certificate store on the appliance
    /// </summary>
    public class CertificateInfo
    {
        private DateTime _notBefore;
        private DateTime _notAfter;

        public CertificateInfo()
        {
            Subject = "";
        }

        /// <summary>
        /// The Subject of the certificate (Read-only)
        /// </summary>
        [ReadOnly(true)]
        public string Subject { get; set; }

        /// <summary>
        /// The CA that issued the certificate (Read-only)
        /// </summary>
        [ReadOnly(true)]
        public string IssuedBy { get; set; }

        /// <summary>
        /// The date the certificate becomes valid (Read-only)
        /// </summary>
        [ReadOnly(true)]
        public DateTime NotBefore
        {
            get => _notBefore;
            set => _notBefore = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        /// <summary>
        /// The date the certificate expires (Read-only)
        /// </summary>
        [ReadOnly(true)]
        public DateTime NotAfter
        {
            get => _notAfter;
            set => _notAfter = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        /// <summary>
        /// The thumbprint of the certificate (Read-only)
        /// </summary>
        [ReadOnly(true)]
        public string Thumbprint { get; set; }

        /// <summary>
        /// Base64 representation of the certificate (write-only)
        /// </summary>
        public string Base64CertificateData { get; set; }

        /// <summary>
        /// Passphase to decode the Base64 representation of the certificate (write-only)
        /// </summary>
        public string Passphrase { get; set; }

        /// <summary>
        /// The certificate is new or existing (Read-only)
        /// </summary>
        [ReadOnly(true)]
        public bool IsNew { get; set; }

        /// <summary>
        /// The certificate has a valid chain of trust (Read-only)
        /// </summary>
        [ReadOnly(true)]
        public bool PassedTrustChainValidation { get; set; }

        protected bool Equals(CertificateInfo other)
        {
            return string.Equals(Subject, other.Subject) && string.Equals(IssuedBy, other.IssuedBy) && NotBefore.Equals(other.NotBefore) && NotAfter.Equals(other.NotAfter) && string.Equals(Thumbprint, other.Thumbprint);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((CertificateInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Subject?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (IssuedBy?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ NotBefore.GetHashCode();
                hashCode = (hashCode * 397) ^ NotAfter.GetHashCode();
                hashCode = (hashCode * 397) ^ (Thumbprint?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return "{ Subject : " + Subject + ", Issuer: " + IssuedBy + ", Thumbprint: " + Thumbprint + " }";
        }
        
    }
}
