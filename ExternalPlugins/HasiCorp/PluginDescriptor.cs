using System.Collections.Generic;
using System.Threading.Tasks;
using OneIdentity.Common;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;

namespace OneIdentity.HashiCorp
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static IVaultClient _vaultClient = null;
        private static Dictionary<string,string> _configuration = null;

        //TODO: The following constants need to come from the configuration
        private string _authToken = "s.MwPuFsJTtyoX8K4sb85AcjrF";
        private string _address = "http://127.0.0.1:8200";
        private string _mountPoint = "secret";
        private string _secretsPath = "oneidentity";

        private readonly string _authTokenName = "authToken";
        private readonly string _addressName = "address";
        private readonly string _mountPointName = "mountPoint";
        private readonly string _secretsPathName = "secretsPath";

        public PluginDescriptor()
        {
        }

        public string Name { get; } = "HashiCorpVault";
        public string Description { get; } = "This is the HashiCorp Vault plugin for updating the passwords";

        public Dictionary<string,string> GetPluginConfiguration()
        {
            if (_configuration == null)
            {
                _configuration = new Dictionary<string, string>();
                _configuration.Add(_authTokenName, "");
                _configuration.Add(_addressName, _address);
                _configuration.Add(_mountPointName, _mountPoint);
                _configuration.Add(_secretsPathName, _secretsPath);
            }

            return _configuration;
        }

        public Dictionary<string,string> SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(_authTokenName) &&
                configuration.ContainsKey(_addressName) && configuration.ContainsKey(_mountPointName) &&
                configuration.ContainsKey(_secretsPathName))
            {
                var authMethod = new TokenAuthMethodInfo(configuration[_authTokenName]);
                var vaultClientSettings = new VaultClientSettings(configuration[_addressName], authMethod);
                _vaultClient = new VaultClient(vaultClientSettings);
                _configuration = configuration;
            }

            return _configuration;
        }

        public bool SetPassword(string account, string password)
        {
            if (_vaultClient == null)
                return false;

            var passwordData = new Dictionary<string, object>();
            passwordData.Add(account, password);

            try
            {
                _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(_secretsPath, passwordData, null, _mountPoint)
                    .Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
