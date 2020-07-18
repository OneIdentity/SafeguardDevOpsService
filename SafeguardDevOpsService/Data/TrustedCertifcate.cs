
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using LiteDB;

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
        /// Base64 representation of the certificate
        /// </summary>
        public string Base64CertificateData { get; set; }

        /// <summary>
        /// Get the certificate information
        /// </summary>
        public CertificateInfo GetCertificateInfo()
        {
            var cert = GetCertificate();
            var builder = new StringBuilder()
                .Append("-----BEGIN CERTIFICATE-----")
                .Append(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.None))
                .Append("-----END CERTIFICATE-----");

            return new CertificateInfo()
            {
                IssuedBy = cert.Issuer,
                NotAfter = cert.NotAfter,
                NotBefore = cert.NotBefore,
                Subject = cert.Subject,
                Thumbprint = cert.Thumbprint,
                Base64CertificateData = builder.ToString()
            };
        }

        /// <summary>
        /// Get the certificate
        /// </summary>
        public X509Certificate2 GetCertificate()
        {
            var certificateBytes = Convert.FromBase64String(Base64CertificateData);
            return new X509Certificate2(certificateBytes);
        }

    }
}
