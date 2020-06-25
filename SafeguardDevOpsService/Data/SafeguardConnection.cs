namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Safeguard appliance connection information
    /// </summary>
    public class SafeguardConnection
    {
        /// <summary>
        /// Safeguard appliance network address
        /// </summary>
        public string ApplianceAddress { get; set; }
        /// <summary>
        /// Should ignore Ssl validation
        /// </summary>
        public bool IgnoreSsl { get; set; }
        /// <summary>
        /// Api version
        /// </summary>
        public int ApiVersion { get; set; }
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
