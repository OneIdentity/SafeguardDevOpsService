namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Safeguard appliance connection information
    /// </summary>
    public class SafeguardConnection : SafeguardData
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
    }
}
