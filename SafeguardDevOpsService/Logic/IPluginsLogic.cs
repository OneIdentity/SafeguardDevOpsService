using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IPluginsLogic
    {
        void InstallPlugin(string base64Plugin);
        void InstallPlugin(IFormFile formFile);

        IEnumerable<Plugin> GetAllPlugins(bool includeDeleted = false);
        IEnumerable<Plugin> GetAllPluginInstancesByName(string name, bool includeDeleted = false);
        Plugin GetPluginByName(string name);
        void DeletePluginByName(string name);
        void DeleteAllPluginInstancesByName(string name);
        Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name);
        Plugin CreatePluginInstanceByName(string name, bool copyConfig);

        IEnumerable<AccountMapping> GetAccountMappings(string name, bool includeAllInstances = false);
        AccountMapping GetAccountMappingById(string name, int accountId);
        IEnumerable<AccountMapping> SaveAccountMappings(ISafeguardConnection sgConnection, string name, IEnumerable<A2ARetrievableAccount> accounts);
        void DeleteAccountMappings(string name);
        void DeleteAccountMappings(string name, IEnumerable<AccountMapping> accounts);
        void DeleteAccountMappings();

        A2ARetrievableAccount GetPluginVaultAccount(ISafeguardConnection sgConnection, string name);
        A2ARetrievableAccount SavePluginVaultAccount(ISafeguardConnection sgConnection, string name, AssetAccount sppAccount);
        void RemovePluginVaultAccount(string name);
        void ClearMappedPluginVaultAccounts();

        void RestartService();
        bool TestPluginConnectionByName(ISafeguardConnection sgConnection, string name);
        PluginState GetPluginDisabledState(string name);
        PluginState UpdatePluginDisabledState(string name, bool isDisabled);
        PluginReverseFlowState GetPluginReverseFlowState(string name);
        PluginReverseFlowState UpdatePluginReverseFlowState(string name, bool isDisabled);
    }
}
