
using System.Collections.Generic;
using LiteDB;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Logic;
using JsonHelper = OneIdentity.DevOps.Logic.JsonHelper;

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
        /// List of credential types that the plugin supports
        /// </summary>
        public CredentialType[] SupportedCredentialTypes { get; set; }

        /// <summary>
        /// Is reverse flow supported in the plugin
        /// </summary>
        public bool SupportsReverseFlow { get; set; }

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
        /// Is this the root plugin
        /// </summary>
        public bool IsRootPlugin { get; set; } = false;

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
        /// Root plugin name
        /// </summary>
        public string RootPluginName => GetRootPluginName(this.Name);


#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public DevOpsSecretsBrokerPlugin ToDevOpsSecretsBrokerPlugin(IConfigurationRepository configDb)
        {
            var devOpsSecretsBrokerPlugin = new DevOpsSecretsBrokerPlugin
            {
                Name = Name, 
                DisplayName = DisplayName,
                Description = Description,
                Version = Version, 
                Configuration = JsonHelper.SerializeObject(Configuration),
                MappedVaultAccounts = VaultAccountId.ToString()
            };

            var accountMappings = configDb.GetAccountMappings(Name);
            if (accountMappings != null)
                devOpsSecretsBrokerPlugin.MappedAccounts = JsonHelper.SerializeObject(accountMappings);

            return devOpsSecretsBrokerPlugin;
        }

        public Plugin()
        {
        }

        public Plugin(DevOpsSecretsBrokerPlugin devOpsPlugin)
        {
            Name = devOpsPlugin.Name;
            DisplayName = devOpsPlugin.DisplayName;
            Description = devOpsPlugin.Description;
            Version = devOpsPlugin.Version;
            Configuration = JsonHelper.DeserializeObject<Dictionary<string,string>>(devOpsPlugin.Configuration);
            if (int.TryParse(devOpsPlugin.MappedVaultAccounts, out var x))
                VaultAccountId = x;
        }

        public static string GetNewPluginInstanceName(string pluginName)
        {
            var pluginId = WellKnownData.GenerateRandomId();
            return pluginId == null ? null : $"{pluginName}-{pluginId}";
        }

        private static string GetPluginId(string pluginName)
        {
            if (string.IsNullOrEmpty(pluginName))
            {
                return null;
            }

            var idDelimeter = pluginName.LastIndexOf('-');
            if (idDelimeter > 0)
            {
                var idStr = pluginName.Substring(pluginName.LastIndexOf('-'));
                if (!string.IsNullOrEmpty(idStr) && idStr.Length == WellKnownData.RandomStringLength + 1)
                {
                    return idStr.Substring(1);
                }
            }

            return null;
        }

        private static string GetRootPluginName(string pluginName)
        {
            var idStr = GetPluginId(pluginName);
            if (idStr != null)
            {
                try
                {
                    return pluginName.Remove(pluginName.Length - (WellKnownData.RandomStringLength + 1));
                }
                catch {}
            }

            return pluginName;
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    }
}
