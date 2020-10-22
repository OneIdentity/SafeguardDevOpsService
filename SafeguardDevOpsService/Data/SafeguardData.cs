namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Safeguard data
    /// </summary>
    public class SafeguardData
    {
        /// <summary>
        /// Safeguard appliance network address
        /// </summary>
        public string ApplianceAddress { get; set; }
        /// <summary>
        /// Api version
        /// </summary>
        public int? ApiVersion { get; set; }
        /// <summary>
        /// Should ignore Ssl verification
        /// </summary>
        public bool? IgnoreSsl { get; set; }
        /// <summary>
        /// Should the database be removed if connecting to a different appliance
        /// </summary>
        public bool? PendingRemoval { get; set; }
    }
}
