using System.Security.Cryptography.X509Certificates;
using LiteDB;
using OneIdentity.DevOps.Extensions;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Represents a trusted certificate
    /// </summary>
    public class TrustedCertificate
    {
        /// <summary>
        /// Certificate thumbprint
        /// </summary>
        [BsonId]
        public string Thumbprint { get; set; }

        /// <summary>
        /// Certificate subject
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Base64 representation of the certificate
        /// </summary>
        public string Base64CertificateData { get; set; }

        /// <summary>
        /// Get the certificate information
        /// </summary>
        public CertificateInfo GetCertificateInfo(bool isNew = true)
        {
            var cert = GetCertificate();
            return new CertificateInfo()
            {
                IssuedBy = cert.Issuer,
                NotAfter = cert.NotAfter,
                NotBefore = cert.NotBefore,
                Subject = cert.Subject,
                Thumbprint = cert.Thumbprint,
                Base64CertificateData = cert.ToPemFormat(),
                IsNew = isNew
            };
        }

        /// <summary>
        /// Get the certificate
        /// </summary>
        public X509Certificate2 GetCertificate()
        {
            var certificateBytes = CertificateHelper.ConvertPemToData(Base64CertificateData);
            return new X509Certificate2(certificateBytes);
        }

    }
}
