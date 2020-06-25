namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Asset-Account information
    /// </summary>
    public class SppAccount
    {
        /// <summary>
        /// Asset-Account information
        /// </summary>
        /// <param name="hasPassword"></param>
        /// <param name="disabled"></param>
        public SppAccount(bool hasPassword, bool disabled)
        {
            HasPassword = hasPassword;
            Disabled = disabled;
        }

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
        /// Account is disabled
        /// </summary>
        public bool Disabled { get; set; }
        /// <summary>
        /// Asset Id
        /// </summary>
        public int SystemId { get; set; }
        /// <summary>
        /// Asset name
        /// </summary>
        public string SystemName { get; set; }
        /// <summary>
        /// Asset network address
        /// </summary>
        public string SystemNetworkAddress { get; set; }
        /// <summary>
        /// Allow password request
        /// </summary>
        public bool AllowPasswordRequest { get; set; }
        /// <summary>
        /// Allow session request
        /// </summary>
        public bool AllowSessionRequest { get; set; }
    }
}
