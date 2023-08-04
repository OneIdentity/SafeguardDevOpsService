using System;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Asset-Account information
    /// </summary>
    public class AssetAccount
    {
        /// <summary>
        /// Account id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Account name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Account description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Domain name
        /// </summary>
        public string DomainName { get; set; }
        /// <summary>
        /// Account has a password
        /// </summary>
        public bool HasPassword { get; set; }
        /// <summary>
        /// The password for the account (write only)
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        /// Account is disabled
        /// </summary>
        public bool Disabled { get; set; }
        /// <summary>
        /// Asset
        /// </summary>
        public Asset Asset { get; set; }
        /// <summary>
        /// Date this entity was created.
        /// </summary>
        public DateTimeOffset CreatedDate { get; set; }
        /// <summary>
        /// The database ID of the user that created this entity.
        /// </summary>
        public int CreatedBy { get; set; }
        /// <summary>
        /// The display name of the user that created this entity.
        /// </summary>
        public string CreatedByUserDisplayName { get; set; }
    }
}
