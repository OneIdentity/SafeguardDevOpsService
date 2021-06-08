
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using LiteDB;
using OneIdentity.DevOps.Common.Annotations;

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

        private bool _isDirty = false;

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
        public bool IsDirty  //TODO: See if there is a way to implement an ObservableCollection rather than using this isDirty property
        {
            get
            {
                return _isDirty;
            }

            set
            {
                if (value != _isDirty)
                {
                    _isDirty = value;
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
