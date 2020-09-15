
namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Represents a server certificate
    /// </summary>
    public class ServerCertificate
    {
        /// <summary>
        /// The Subject of the certificate
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// The thumbprint of the certificate
        /// </summary>
        public string Thumbprint { get; set; }

        /// <summary>
        /// Base64 encoded certificate DER data
        /// </summary>
        public string Base64CertificateData { get; set; }

    }
}
