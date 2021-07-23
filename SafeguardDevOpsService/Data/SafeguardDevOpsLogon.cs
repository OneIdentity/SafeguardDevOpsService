
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

    }
}
