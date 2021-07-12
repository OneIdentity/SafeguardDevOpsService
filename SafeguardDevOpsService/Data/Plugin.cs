
using LiteDB;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Logic;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Represents a DevOps plugin
    /// </summary>
    public class Plugin : PluginConfiguration
    {
        /// <summary>
        /// Name of the plugin
        /// </summary>
        [BsonId]
        public string Name { get; set; }

        /// <summary>
        /// Display name of the plugin
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Description of the plugin
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Base64 representation of the plugin (write-only)
        /// </summary>
        public string Base64PluginData { get; set; }

        /// <summary>
        /// A2A registration vault account id
        /// </summary>
        public int? VaultAccountId { get; set; }

        /// <summary>
        /// Is the plugin loaded
        /// </summary>
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Is the plugin loaded
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Is the plugin disabled
        /// </summary>
        public bool IsDisabled { get; set; } = false;

        /// <summary>
        /// Is the plugin system owned
        /// </summary>
        public bool IsSystemOwned { get; set; } = false;

        /// <summary>
        /// Plugin version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Mapped accounts count
        /// </summary>
        public int MappedAccountsCount { get; set; }

        /// <summary>
        /// Convert to DevOpsSecretsBrokerPlugin
        /// </summary>
        /// <param name="pluginsLogic"></param>
        /// <returns></returns>
        public DevOpsSecretsBrokerPlugin ToDevOpsSecretsBrokerPlugin(IPluginsLogic pluginsLogic)
        {
            var devOpsSecretsBrokerPlugin = new DevOpsSecretsBrokerPlugin
            {
                Name = Name, 
                Version = Version, 
                Configuration = JsonHelper.SerializeObject(Configuration),
                MappedVaultAccounts = VaultAccountId.ToString()
            };

            var accountMappings = pluginsLogic.GetAccountMappings(Name);
            if (accountMappings != null)
                devOpsSecretsBrokerPlugin.MappedAccounts = JsonHelper.SerializeObject(accountMappings);

            return devOpsSecretsBrokerPlugin;
        }
    }
}
