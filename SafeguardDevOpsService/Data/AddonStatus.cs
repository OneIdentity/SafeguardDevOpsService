
using System.Collections.Generic;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Add-on status
    /// </summary>
    public class AddonStatus
    {
        /// <summary>
        /// Is the addon ready
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// Health status messages
        /// </summary>
        public List<string> HealthStatus { get; set; }
    }
}
