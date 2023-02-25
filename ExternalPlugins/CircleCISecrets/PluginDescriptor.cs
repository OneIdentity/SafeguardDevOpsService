using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneIdentity.DevOps.Common;
using RestSharp;
using Serilog;

namespace OneIdentity.DevOps.HashiCorpVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static VaultConnection _secretsClient;
        private static Dictionary<string,string> _configuration;
        private static ILogger _logger;
        private static Regex _rgx;
        private static string _address = "http://circleci.com/api/v2";
        private ContextItem _contextItem = null;

        private const string OranizationId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
        private const string Context = "My Context";

        private const string OrganizationIdName = "Organization Id";
        private const string ContextName = "Context Name";

        public string Name => "CircleCISecrets";
        public string DisplayName => "CircleCI Secrets";
        public string Description => "This is the CircleCI Secrets plugin for updating passwords";

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { OrganizationIdName, OranizationId },
                { ContextName, Context }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(OrganizationIdName) && configuration.ContainsKey(ContextName))
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
                    _secretsClient = new VaultConnection(_address, credential, _logger);
                    _logger.Information($"Plugin {Name} successfully authenticated.");
                }
                catch (Exception ex)
                {
                    _logger.Information(ex, $"Invalid configuration for {Name}. Please use the api to set a valid configuration. {ex.Message}");
                }

                try
                {
                    var response = _secretsClient.InvokeMethodFull(Method.GET, $"/context?owner-id={_configuration[OrganizationIdName]}");
                    var contexts = DeserializeObject<ContextItems>(response.Body);
                    if (contexts?.items != null)
                    {
                        _contextItem = contexts.items.ToArray().FirstOrDefault(x => x.name.Equals(_configuration[ContextName]), null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
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

            if (_contextItem == null)
            {
                _logger.Information($"Organization context {_configuration[ContextName]} not found for {DisplayName}.");
                return false;
            }

            // If we got a context, then we were able to connect using the credentials during the
            //  SetVaultCredential() callback above.
            return true;
        }

        public bool SetPassword(string asset, string account, string password, string altAccountName = null)
        {
            if (_configuration == null || _secretsClient == null || _contextItem == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}_{account}", "_");
            try
            {
                var payload = "{\"value\":\""+password+"\"}";
                var response = _secretsClient.InvokeMethodFull(Method.PUT, $"/context/{_contextItem.id}/environment-variable/{name}", payload);

                _logger.Information($"Password for {name} has been successfully stored in the context.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to set the secret for {name}: {ex.Message}.");
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
            _secretsClient = null;
            _configuration.Clear();
            _configuration = null;
        }

        private T DeserializeObject<T>(string rawJson) where T : class
        {
            T dataTransferObject = JsonConvert.DeserializeObject<T>(rawJson,
                new JsonSerializerSettings
                {
                    Error = HandleDeserializationError,
                    DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
                });

            if (dataTransferObject == null)
            {
                _logger.Error($"Failed to get the contexts for the organization {OranizationId}.");
            }
            return dataTransferObject;
        }

        private void HandleDeserializationError(object sender, ErrorEventArgs errorArgs)
        {
            var currentError = errorArgs.ErrorContext.Error.Message;
            _logger.Error(currentError);
            errorArgs.ErrorContext.Handled = true;
        }

        class ContextItems
        {
            public List<ContextItem> items { get; set; }
            public string next_page_token { get; set; }
        }

        class ContextItem
        {
            public string id { get; set; }
            public string name { get; set; }
        }
    }
}
