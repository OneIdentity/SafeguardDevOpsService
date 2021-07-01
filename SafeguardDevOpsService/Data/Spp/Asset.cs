using System;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Represents an asset
    /// </summary>
    public class Asset
    {
        /// <summary>
        /// Database ID of system (Read-only)
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Description of the asset
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// ID of platform type / version
        /// </summary>
        public int PlatformId { get; set; }

        /// <summary>
        /// Display name platform
        /// </summary>
        public string PlatformDisplayName { get; set; }

        /// <summary>
        /// Date this entity was created
        /// </summary>
        public DateTimeOffset CreatedDate { get; set; }

        /// <summary>
        /// The ID of the user that created this entity
        /// </summary>
        public int CreatedByUserId { get; set; }

        /// <summary>
        /// The display name of the user that created this entity
        /// </summary>
        public string CreatedByUserDisplayName { get; set; }

        /// <summary>
        /// The name of the asset
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Network DNS name or IP address
        /// </summary>
        public string NetworkAddress { get; set; }
        
    }
}

