
namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Type of CSR to create
    /// </summary>
    public enum CertificateType
    {
        /// <summary>
        /// A2A client certificate
        /// </summary>
        A2AClient,
        /// <summary>
        /// Web Ssl certificate
        /// </summary>
        WebSsl,
        /// <summary>
        /// Trusted certificate
        /// </summary>
        Trusted
    }
}
