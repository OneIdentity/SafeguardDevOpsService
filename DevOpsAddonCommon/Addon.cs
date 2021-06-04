using LiteDB;

namespace OneIdentity.DevOps.Common
{
    /// <summary>
    /// Represents a Secrets Broker AddOn
    /// </summary>
    public class AddOn
    {
        /// <summary>
        /// Name of the AddOn
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
        public AddOnManifest Manifest { get; set; }
    }
}
