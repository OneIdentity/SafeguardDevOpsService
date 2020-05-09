using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using LiteDB;

namespace OneIdentity.DevOps.Data
{
    [DataContract]
    public class AccountMapping
    {
        private string _apiKey;
        private string _vaultName;

        [BsonId]
        public string Key
        {
            get => _apiKey+_vaultName;
        }

        [DataMember]
        public string ApiKey
        {
            get => _apiKey;
            set => _apiKey = value;
        }
        [DataMember]
        public string AssetName { get; set; }
        [DataMember]
        public string NetworkAddress { get; set; }
        [DataMember]
        public string AccountName { get; set; }
        [DataMember]
        public string DomainName { get; set; }
        [DataMember]
        public string VaultName
        {
            get => _vaultName; 
            set => _vaultName = value;
        }

        public bool Equals(AccountMapping other)
        {
            return (string.Compare(AssetName, other.AssetName, StringComparison.OrdinalIgnoreCase) == 0) && 
                   (string.Compare(AccountName, other.AccountName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (string.Compare(DomainName, other.DomainName, StringComparison.OrdinalIgnoreCase) == 0) &&
                   (string.Compare(VaultName, other.VaultName, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
