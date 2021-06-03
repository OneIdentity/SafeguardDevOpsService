
using System.Collections.Generic;
using LiteDB;

namespace OneIdentity.DevOps.Common
{
    /// <summary>
    /// Represents a Secrets Broker addon
    /// </summary>
    public class Addon
    {
        /// <summary>
        /// Name of the addon
        /// </summary>
        [BsonId]
        public string Name { get; set; }

        /// <summary>
        /// A2A registration vault account id
        /// </summary>
        public int? VaultAccountId { get; set; }

        /// <summary>
        /// Vault Account Name
        /// </summary>
        public string VaultAccountName { get; set; }

        /// <summary>
        /// Vault Asset Id
        /// </summary>
        public int VaultAssetId { get; set; }

        /// <summary>
        /// Vault Asset name
        /// </summary>
        public string VaultAssetName { get; set; }

        /// <summary>
        /// Vault Credentials
        /// </summary>
        public AddonManifest Manifest { get; set; }
    }
}
