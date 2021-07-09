
using System;
using System.Diagnostics.CodeAnalysis;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Represents a devOps secrets broker plugin.
    /// </summary>
    public class DevOpsSecretsBrokerPlugin
    {
        /// <summary>
        /// Name of the plugin
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Plugin configuration
        /// </summary>
        public string Configuration { get; set; }

        /// <summary>
        /// Mapped accounts
        /// </summary>
        public string MappedAccounts { get; set; }

        /// <summary>
        /// Mapped vault account
        /// </summary>
        public string MappedVaultAccounts { get; set; }

        /// <summary>
        /// Plugin version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Convert to string.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DevOpsSecretsBrokerPlugin) obj);
        }

        protected bool Equals(DevOpsSecretsBrokerPlugin other)
        {
            return (Name.Equals(other.Name, StringComparison.InvariantCultureIgnoreCase) 
                    && Version.Equals(other.Version, StringComparison.InvariantCultureIgnoreCase)
                    && Configuration.Equals(other.Configuration, StringComparison.InvariantCultureIgnoreCase)
                    && MappedAccounts.Equals(other.MappedAccounts, StringComparison.InvariantCultureIgnoreCase)
                    && MappedVaultAccounts.Equals(other.MappedVaultAccounts, StringComparison.InvariantCultureIgnoreCase));
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name.GetHashCode();
                hashCode = (hashCode * 397) ^ Version.GetHashCode();
                hashCode = (hashCode * 397) ^ Configuration.GetHashCode();
                hashCode = (hashCode * 397) ^ MappedAccounts.GetHashCode();
                hashCode = (hashCode * 397) ^ MappedVaultAccounts.GetHashCode();
                return hashCode;
            }
        }
    }
}
