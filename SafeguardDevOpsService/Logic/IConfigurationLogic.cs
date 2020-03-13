using System.Collections.Generic;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;

namespace OneIdentity.DevOps.Logic
{
    public interface IConfigurationLogic
    {
        ManagementConnectionData InitialConfiguration(InitialConfiguration initialConfig);
        void DeleteConfiguration();
        Registration GetRegistration();
        ManagementConnectionData UpdateConnectionConfiguration(ConnectionConfiguration connectionConfig);
        IEnumerable<AccountMapping> GetAccountMappings(string accountName, string vaultName);
        IEnumerable<AccountMapping> SaveAccountMappings(IEnumerable<AccountMapping> newAccountMappings);
        IEnumerable<AccountMapping> RemoveAccountMappings(bool removeAll, string accountName, string vaultName);
        IEnumerable<RetrievableAccount> GetRetrievableAccounts();
        void EnableMonitoring(bool enable);
        IEnumerable<Plugin> GetAllPlugins();
        Plugin GetPluginByName(string name);
        Plugin SavePluginConfigurationByName(PluginConfiguration pluginConfiguration, string name);
        void DeletePluginByName(string name);
    }
}
