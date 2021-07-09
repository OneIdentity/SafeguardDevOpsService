using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using LiteDB;

namespace OneIdentity.DevOps.Common
{
    /// <summary>
    /// Represents a Secrets Broker addon
    /// </summary>
    public class Addon: INotifyPropertyChanged
    {
        /// <summary>
        /// Name of the addon
        /// </summary>
        [BsonId]
        public string Name { get; set; }

        /// <summary>
        /// Base64 representation of the addon (write-only)
        /// </summary>
        public string Base64AddonData { get; set; }

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

        private bool _credentialsUpdated = false;

        /// <summary>
        /// Vault Credentials
        /// </summary>
        ///
        [IgnoreDataMember]
        public Dictionary<string,string> VaultCredentials { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Current State of the Object
        /// </summary>
        [IgnoreDataMember]
        public bool CredentialsUpdated  
        {
            get => _credentialsUpdated;

            set
            {
                if (value != _credentialsUpdated)
                {
                    _credentialsUpdated = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
