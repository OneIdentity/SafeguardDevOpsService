
namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Service logon
    /// </summary>
    public class SafeguardDevOpsLogon
    {
        /// <summary>
        /// Safeguard connection information
        /// </summary>
        public SafeguardDevOpsConnection SafeguardDevOpsConnection { get; set; }

        /// <summary>
        /// Has available A2A registrations
        /// </summary>
        public bool HasAvailableA2ARegistrations { get; set; }

        /// <summary>
        /// Has missing plugins
        /// </summary>
        public bool HasMissingPlugins { get; set; }

        /// <summary>
        /// Needs client certificate
        /// </summary>
        public bool NeedsClientCertificate { get; set; }

        /// <summary>
        /// Needs web certificate
        /// </summary>
        public bool NeedsWebCertificate { get; set; }

        /// <summary>
        /// Needs Trusted certificates
        /// </summary>
        public bool NeedsTrustedCertificates { get; set; }

        /// <summary>
        /// Needs SSL enabled
        /// </summary>
        public bool NeedsSSLEnabled { get; set; }

        /// <summary>
        /// Passed Trust Chain Validation
        /// </summary>
        public bool PassedTrustChainValidation { get; set; }

        /// <summary>
        /// Is reverse flow available
        /// </summary>
        public bool ReverseFlowAvailable { get; set; }
    }
}
