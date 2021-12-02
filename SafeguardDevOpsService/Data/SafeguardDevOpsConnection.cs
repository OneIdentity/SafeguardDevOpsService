namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Safeguard appliance connection information
    /// </summary>
    public class SafeguardDevOpsConnection : SafeguardData
    {
        /// <summary>
        /// Safeguard appliance Id
        /// </summary>
        public string ApplianceId { get; set; }
        /// <summary>
        /// Safeguard appliance name
        /// </summary>
        public string ApplianceName { get; set; }
        /// <summary>
        /// Safeguard appliance version
        /// </summary>
        public string ApplianceVersion { get; set; }
        /// <summary>
        /// Safeguard appliance current state
        /// </summary>
        public string ApplianceState { get; set; }
        /// <summary>
        /// Safeguard appliance the DevOps endpoints
        /// </summary>
        public bool ApplianceSupportsDevOps { get; set; } = false;
        /// <summary>
        /// Safeguard DevOps instance Id
        /// </summary>
        public string DevOpsInstanceId { get; set; }
        /// <summary>
        /// Logged in user name
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// Logged in user display name
        /// </summary>
        public string UserDisplayName { get; set; }
        /// <summary>
        /// Logged in user roles
        /// </summary>
        public string[] AdminRoles { get; set; }
        /// <summary>
        /// Safeguard DevOps version
        /// </summary>
        public string Version { get; set; }
    }
}
