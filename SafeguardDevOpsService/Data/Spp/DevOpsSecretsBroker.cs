using System;
using System.Collections.Generic;
using System.Linq;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Represents an DevOps registration.
    /// </summary>
    public class DevOpsSecretsBroker
    {
        private IEnumerable<DevOpsSecretsBrokerAccount> _accounts = Enumerable.Empty<DevOpsSecretsBrokerAccount>();
        private IEnumerable<DevOpsSecretsBrokerPlugin> _plugins = Enumerable.Empty<DevOpsSecretsBrokerPlugin>();

        /// <summary>
        /// DevOps secrets broker registration Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The Secrets Broker host.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The Secrets Broker asset id.
        /// </summary>
        public int AssetId { get; set; }

        /// <summary>
        /// The Secrets Broker asset name.
        /// </summary>
        public string AssetName { get; set; }

        /// <summary>
        /// The accounts to plugins mapping.
        /// </summary>
        public string AccountMapping { get; set; }

        /// <summary>
        /// The accounts to plugins mapping.
        /// </summary>
        public string VaultAccountMapping { get; set; }

        /// <summary>
        /// The devops instance id.
        /// </summary>
        public string DevOpsInstanceId { get; set; }

        /// <summary>
        /// The A2A Registration.
        /// </summary>
        public A2ARegistration A2ARegistration { get; set; }

        /// <summary>
        /// Set of asset accounts that are assigned to this devops secrets broker
        /// </summary>
        public IEnumerable<DevOpsSecretsBrokerAccount> Accounts
        {
            get => _accounts;
            set => _accounts = value;
        }

        /// <summary>
        /// Set of plugins that are assigned to this devops secrets broker
        /// </summary>
        public IEnumerable<DevOpsSecretsBrokerPlugin> Plugins
        {
            get => _plugins;
            set => _plugins = value;
        }

        /// <summary>
        /// Date this entity was created
        /// </summary>
        public DateTimeOffset CreatedDate { get; set; }

        /// <summary>
        /// The database ID of the user that created this entity
        /// </summary>
        public int CreatedByUserId { get; set; }

        /// <summary>
        /// The display name of the user that created this entity
        /// </summary>
        public string CreatedByUserDisplayName { get; set; }
    }
}
