using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IPluginsLogic
    {
        void InstallPlugin(string base64Plugin);
        void InstallPlugin(IFormFile formFile);

        IEnumerable<Plugin> GetAllPlugins(bool includeDeleted = false);
        Plugin GetPluginByName(string name);
        void DeletePluginByName(string name);
        Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name);

        IEnumerable<AccountMapping> GetAccountMappings(string name);
        AccountMapping GetAccountMappingById(string name, int accountId);
        IEnumerable<AccountMapping> SaveAccountMappings(string name, IEnumerable<A2ARetrievableAccount> mappings);
        void DeleteAccountMappings(string name);
        void DeleteAccountMappings(string name, IEnumerable<AccountMapping> accounts);
        void DeleteAccountMappings();

        A2ARetrievableAccount GetPluginVaultAccount(string name);
        A2ARetrievableAccount SavePluginVaultAccount(string name, AssetAccount sppAccount);
        void RemovePluginVaultAccount(string name);

        void RestartService();
        bool TestPluginConnectionByName(string name);
    }
}
