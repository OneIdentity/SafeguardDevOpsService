using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;

namespace OneIdentity.DevOps.Logic
{
    public interface IPluginsLogic
    {
        void InstallPlugin(string base64Plugin);
        void InstallPlugin(IFormFile formFile);

        IEnumerable<Plugin> GetAllPlugins();
        Plugin GetPluginByName(string name);
        void DeletePluginByName(string name);
        Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name);

        IEnumerable<AccountMapping> GetAccountMappings(string name);
        IEnumerable<AccountMapping> SaveAccountMappings(string name, IEnumerable<A2ARetrievableAccount> mappings);
        void DeleteAccountMappings(string name);
        void DeleteAccountMappings();

        void RestartService();
    }
}
