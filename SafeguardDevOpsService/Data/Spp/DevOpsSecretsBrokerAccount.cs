using System;
using System.Runtime.Serialization;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Represents a devOps secrets broker account.
    /// </summary>
    public class DevOpsSecretsBrokerAccount
    {
        /// <summary>
        /// The database ID of the account.
        /// </summary>
        public int AccountId { get; set; }

        /// <summary>
        /// The name of the account
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// The password for the account (write only)
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// A short description of the account.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The distinguished name of the account
        /// </summary>
        public string DistinguishedName { get; set; }

        /// <summary>
        /// The database ID of the asset this account is associated with
        /// </summary>
        public int AssetId { get; set; }

        /// <summary>
        /// The name of the asset this account is associated with
        /// </summary>
        public string AssetName { get; set; }

        /// <summary>
        /// The network address of the asset this account is associated with
        /// </summary>
        public string AssetNetworkAddress { get; set; }

        /// <summary>
        /// The domain name this account is associated with
        /// </summary>
        public string DomainName { get; set; }

        /// <summary>
        /// The domain name this account is associated with
        /// </summary>
        public string NetbiosName { get; set; }

        /// <summary>
        /// Name of the AssetPartition this account belongs to
        /// </summary>
        public string AssetPartitionName { get; set; }

        /// <summary>
        /// The database ID of the type of platform.
        /// </summary>
        public int PlatformId { get; set; }

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

        public override string ToString()
        {
            return AccountName;
        }
    }
}
