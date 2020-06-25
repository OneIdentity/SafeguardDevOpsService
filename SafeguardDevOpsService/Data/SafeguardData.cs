namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Safeguard data
    /// </summary>
    public class SafeguardData
    {
        /// <summary>
        /// Network address
        /// </summary>
        public string NetworkAddress { get; set; }
        /// <summary>
        /// Api version
        /// </summary>
        public int? ApiVersion { get; set; }
        /// <summary>
        /// Should ignore Ssl verification
        /// </summary>
        public bool? IgnoreSsl { get; set; }
    }
}
