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
        private static HttpClient _secretsClient;
        private static Dictionary<string,string> _configuration;
        private static ILogger _logger;
        private static Regex _rgx;

        private const string Address = "https://127.0.0.1:8443";
        private const string User = "User Name";

        private const string AddressName = "address";
        private const string UserName = "username";

        public string Name => "JenkinsSecrets";
        public string DisplayName => "Jenkins Secrets";
        public string Description => "This is the Jenkins Secrets plugin for updating passwords";

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
                _logger.Information($"Plugin {Name} has been successfully configured.");
                _rgx = new Regex("[^a-zA-Z0-9-]");
            }
            else
            {
                _logger.Error("Some parameters are missing from the configuration.");
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
                    client.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded");

                    _logger.Information($"Plugin {Name} successfully authenticated.");
                }
                catch (Exception ex)
                {
                    _logger.Information(ex, $"Invalid configuration for {Name}. Please use the api to set a valid configuration. {ex.Message}");
                }
            }
            else
            {
                _logger.Error("The plugin configuration or credential is missing.");
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
                _logger.Information($"Test vault connection for {DisplayName}: Result = {result}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
                return false;
            }
        }

        public bool SetPassword(string asset, string account, string password, string altAccountName = null)
        {
            if (_secretsClient == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            try
            {
                var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");
                var id = altAccountName ?? $"{asset}{account}";

                var response = _secretsClient.GetAsync($"credentials/store/system/domain/_/credential/{id}/").Result;
                if (response.IsSuccessStatusCode)
                {
                    var payload = $"{{\"stapler-class\": \"com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl\", \"scope\": \"GLOBAL\", \"username\": \"{name}\", \"password\": \"{password}\", \"id\": \"{id}\", \"description\": \"{name}\"}}";
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string,string>("json",payload)
                    });

                    response = _secretsClient.PostAsync($"credentials/store/system/domain/_/credential/{id}/updateSubmit", content).Result;
                }
                else
                {
                    var payload = $"{{\"\": \"0\", \"credentials\": {{\"scope\": \"GLOBAL\",\"id\": \"{id}\",\"username\": \"{name}\",\"password\": \"{password}\", \"description\": \"{name}\",\"$class\": \"com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl\"}}}}";
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string,string>("json",payload)
                    });

                    response = _secretsClient.PostAsync("credentials/store/system/domain/_/createCredentials", content).Result;
                }

                response = _secretsClient.GetAsync($"credentials/store/system/domain/_/credential/{id}/").Result;
                response.EnsureSuccessStatusCode();

                _logger.Information($"Password for {asset}-{altAccountName ?? account} has been successfully stored in the vault.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to set the secret for {asset}-{altAccountName ?? account}: {ex.Message}.");
                return false;
            }
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Unload()
        {
            _logger = null;
            _secretsClient?.Dispose();
            _secretsClient = null;
            _configuration.Clear();
            _configuration = null;
        }
    }
}
