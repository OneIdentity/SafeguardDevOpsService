using System;
using System.Runtime.Serialization;
using LiteDB;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Account mapping
    /// </summary>
    [DataContract]
    public class AccountMapping
    {
        private string _apiKey;
        private string _vaultName;

        /// <summary>
        /// Mapping key
        /// </summary>
        [BsonId]
        public string Key
        {
            get => _apiKey+_vaultName;
        }

        /// <summary>
        /// Api key
        /// </summary>
        [DataMember]
        public string ApiKey
        {
            get => _apiKey;
            set => _apiKey = value;
        }
        /// <summary>
        /// Asset name
        /// </summary>
        [DataMember]
        public string AssetName { get; set; }
        /// <summary>
        /// Asset network address
        /// </summary>
        [DataMember]
        public string NetworkAddress { get; set; }
        /// <summary>
        /// Account name
        /// </summary>
        [DataMember]
        public string AccountName { get; set; }
        /// <summary>
        /// Domain name
        /// </summary>
        [DataMember]
        public string DomainName { get; set; }
        /// <summary>
        /// Vault name
        /// </summary>
        [DataMember]
        public string VaultName
        {
            get => _vaultName; 
            set => _vaultName = value;
        }

        /// <summary>
        /// Equals
        /// </summary>
        public bool Equals(AccountMapping other)
        {
            return (string.Compare(AssetName, other.AssetName, StringComparison.OrdinalIgnoreCase) == 0) && 
                   (string.Compare(AccountName, other.AccountName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (string.Compare(DomainName, other.DomainName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (string.Compare(VaultName, other.VaultName, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
