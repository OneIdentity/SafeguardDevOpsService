using System;
using System.Collections.Generic;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Represents a collection of assets and accounts along with management configuration
    /// </summary>
    public class AssetPartition
    {
        /// <summary>
        /// Database ID of the AssetPartition
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the partition
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the partition
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// List of Identities that own this partition
        /// </summary>
        public IEnumerable<Identity> ManagedBy { get; set; }

        /// <summary>
        /// Date this entity was created (Read-only)
        /// </summary>
        public DateTimeOffset CreatedDate { get; set; }

        /// <summary>
        /// The database ID of the user that created this entity (Read-only)
        /// </summary>
        public int CreatedByUserId { get; set; }

        /// <summary>
        /// The display name of the user that created this entity (Read-only)
        /// </summary>
        public string CreatedByUserDisplayName { get; set; }
    }
}
