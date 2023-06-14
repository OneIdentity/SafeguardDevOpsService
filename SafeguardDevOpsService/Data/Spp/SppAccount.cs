namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Asset-Account information
    /// </summary>
    public class SppAccount
    {
        /// <summary>
        /// Account Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Account Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Domain Name
        /// </summary>
        public string DomainName { get; set; }
        /// <summary>
        /// Account has a password
        /// </summary>
        public bool HasPassword { get; set; }
        /// <summary>
        /// Account has a password
        /// </summary>
        public bool HasSshKey { get; set; }
        /// <summary>
        /// Account is disabled
        /// </summary>
        public bool Disabled { get; set; }
        /// <summary>
        /// Asset 
        /// </summary>
        public Asset Asset { get; set; }
    }
}
