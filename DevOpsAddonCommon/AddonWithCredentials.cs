
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OneIdentity.DevOps.Common.Annotations;

namespace OneIdentity.DevOps.Common
{
    /// <summary>
    /// Represents a Secrets Broker addon
    /// </summary>
    public class AddonWithCredentials: Addon, INotifyPropertyChanged
    {
        private bool _isDirty = false;

        /// <summary>
        /// Vault Credentials
        /// </summary>
        public Dictionary<string,string> VaultCredentials { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Current State of the Object
        /// </summary>
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
