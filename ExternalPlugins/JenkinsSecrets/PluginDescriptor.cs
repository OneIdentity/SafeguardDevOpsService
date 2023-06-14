using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.JenkinsSecrets
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private HttpClient _secretsClient;
        private Dictionary<string,string> _configuration;
        private Regex _rgx;

        private const string Address = "https://127.0.0.1:8443";
        private const string User = "User Name";

        private const string AddressName = "address";
        private const string UserName = "username";

        public string Name => "JenkinsSecrets";
        public string DisplayName => "Jenkins Secrets";
        public string Description => "This is the Jenkins Secrets plugin for updating passwords";
        public bool SupportsReverseFlow => false;

        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

        HttpClientHandler _handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, certChain, policyErrors) => true
        };


        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { AddressName, Address },
                { UserName, User }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(AddressName)&& configuration.ContainsKey(UserName))
            {
                _configuration = configuration;
                Logger.Information($"Plugin {Name} has been successfully configured.");
                _rgx = new Regex("[^a-zA-Z0-9-]");
            }
            else
            {
                Logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            if (_configuration != null && credential != null)
            {

                try
                {
                    var client = new HttpClient(_handler)
                    {
                        BaseAddress = new Uri(_configuration[AddressName])
                    };
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", 
                        Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_configuration[UserName]}:{credential}")));

                    var response = client.GetAsync("crumbIssuer/api/xml?xpath=concat(//crumbRequestField,\":\",//crumb)").Result;

                    response.EnsureSuccessStatusCode();

                    var result = response.Content.ReadAsStringAsync().Result;

                    _secretsClient = client;
                    var crumb = result.Split(':');
                    client.DefaultRequestHeaders.Add(crumb[0], crumb[1]);

                    Logger.Information($"Plugin {Name} successfully authenticated.");
                }
                catch (Exception ex)
                {
                    Logger.Information(ex, $"Invalid configuration for {Name}. Please use the api to set a valid configuration. {ex.Message}");
                }
            }
            else
            {
                Logger.Error("The plugin configuration or credential is missing.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_secretsClient == null)
                return false;

            try
            {
                var task = Task.Run(async () => await _secretsClient.GetAsync($"api/"));
                var result = task.Result;
                Logger.Information($"Test vault connection for {DisplayName}: Result = {result}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
                return false;
            }
        }

        public string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName)
        {
            switch (credentialType)
            {
                case CredentialType.Password:
                    return GetPassword(asset, account, altAccountName);
                case CredentialType.SshKey:
                    return GetSshKey(asset, account, altAccountName);
                case CredentialType.ApiKey:
                    Logger.Error($"The {DisplayName} plugin instance does not fetch the ApiKey credential type.");
                    break;
                default:
                    Logger.Error($"Invalid credential type requested from the {DisplayName} plugin instance.");
                    break;
            }

            return null;
        }

        public string SetCredential(CredentialType credentialType, string asset, string account, string[] credential, string altAccountName)
        {
            switch (credentialType)
            {
                case CredentialType.Password:
                    return SetPassword(asset, account, credential, altAccountName);
                case CredentialType.SshKey:
                    return SetSshKey(asset, account, credential, altAccountName);
                case CredentialType.ApiKey:
                    return SetApiKey(asset, account, credential, altAccountName);
                default:
                    Logger.Error($"Invalid credential type sent to the {DisplayName} plugin instance.");
                    break;
            }

            return null;
        }

        public void Unload()
        {
            Logger = null;
            _secretsClient?.Dispose();
            _secretsClient = null;
            _configuration.Clear();
            _configuration = null;
        }

        private string GetPassword(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            return null;
        }

        private string GetSshKey(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            return null;
        }

        private string SetPassword(string asset, string account, string[] password, string altAccountName)
        {
            if (!ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            if (_secretsClient == null)
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            if (password is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            try
            {
                var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");
                var id = altAccountName ?? $"{asset}{account}";

                var response = _secretsClient.GetAsync($"credentials/store/system/domain/_/credential/{id}/").Result;
                if (response.IsSuccessStatusCode)
                {
                    var payload = $"{{\"stapler-class\": \"com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl\", \"scope\": \"GLOBAL\", \"username\": \"{name}\", \"password\": \"{password[0]}\", \"id\": \"{id}\", \"description\": \"{name}\"}}";
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string,string>("json",payload)
                    });
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                    response = _secretsClient.PostAsync($"credentials/store/system/domain/_/credential/{id}/updateSubmit", content).Result;
                }
                else
                {
                    var payload = $"{{\"\": \"0\", \"credentials\": {{\"scope\": \"GLOBAL\",\"id\": \"{id}\",\"username\": \"{name}\",\"password\": \"{password[0]}\", \"description\": \"{name}\",\"$class\": \"com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl\"}}}}";
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string,string>("json",payload)
                    });
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                    response = _secretsClient.PostAsync("credentials/store/system/domain/_/createCredentials", content).Result;
                }

                response = _secretsClient.GetAsync($"credentials/store/system/domain/_/credential/{id}/").Result;
                response.EnsureSuccessStatusCode();

                Logger.Information($"Password for {asset}-{altAccountName ?? account} has been successfully stored in the vault.");
                return password[0];
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set the secret for {asset}-{altAccountName ?? account}: {ex.Message}.");
                return null;
            }
        }

        private string SetSshKey(string asset, string account, string[] sshKey, string altAccountName)
        {
            if (!ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            return null;
        }

        private string SetApiKey(string asset, string account, string[] apiKeys, string altAccountName)
        {
            if (!ValidationHelper.CanHandleApiKey(this))
            {
                return null;
            }

            return null;
        }
    }
}
