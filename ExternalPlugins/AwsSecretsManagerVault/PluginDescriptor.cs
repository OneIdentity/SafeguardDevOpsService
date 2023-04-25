using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OneIdentity.DevOps.Common;
using Serilog;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Threading.Tasks;
using Amazon.Runtime.Internal;
using System.Security.Principal;
using System.Xml.Linq;


namespace OneIdentity.DevOps.AwsSecretsManagerVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private AmazonSecretsManagerClient _awsClient;
        private Dictionary<string, string> _configuration;
        private ILogger _logger;
        private Regex _rgx;

        private const string AccessKeyId = "accessKeyId";
        private const string AwsRegion = "awsRegion";
        
        public string Name => "AwsSecretsManagerVault";
        public string DisplayName => "AWS Secrets Manager Vault";
        public string Description => "This is the AWS Secrets Manager Vault plugin for updating passwords";
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;

        private bool ConfigurationIsValid => _configuration != null &&
            _configuration.ContainsKey(AccessKeyId) &&
            _configuration.ContainsKey(AwsRegion);

        public Dictionary<string, string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { AccessKeyId, "" },
                { AwsRegion, "" }
            };
        }

        public bool SetPassword(string asset, string account, string password, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.Password)
            {
                _logger.Error("This plugin instance does not handle the Password credential type.");
                return false;
            }

            if (_awsClient == null || !ConfigurationIsValid)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            return StoreCredential(_rgx.Replace(altAccountName ?? $"{asset}-{account}", "-"), password);
        }

        public bool SetSshKey(string asset, string account, string sshKey, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.SshKey)
            {
                _logger.Error("This plugin instance does not handle the SshKey credential type.");
                return false;
            }

            if (_awsClient == null || !ConfigurationIsValid)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            return StoreCredential(_rgx.Replace(altAccountName ?? $"{asset}-{account}", "-")+"-sshkey", sshKey);
        }

        public bool SetApiKey(string asset, string account, string[] apiKeys, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.ApiKey)
            {
                _logger.Error("This plugin instance does not handle the ApiKey credential type.");
                return false;
            }

            if (_awsClient == null || !ConfigurationIsValid)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");
            var retval = true;

            foreach (var apiKeyJson in apiKeys)
            {
                var apiKey = JsonHelper.DeserializeObject<ApiKey>(apiKeyJson);
                if (apiKey != null)
                {
                    StoreCredential($"{name}-{apiKey.Name}", $"{apiKey.ClientId}:{apiKey.ClientSecret}");
                }
                else
                {
                    _logger.Error($"The ApiKey {name} {apiKey.ClientId} failed to save to the {this.DisplayName} vault.");
                    retval = false;
                }
            }

            return retval;
        }

        private bool StoreCredential(string secretId, string secret)
        {
            try
            {
                var request = new PutSecretValueRequest()
                {
                    SecretId = secretId,
                    SecretString = secret
                };

                var res = Task.Run(async () => await _awsClient.PutSecretValueAsync(request));

                if (res.Result.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.Information($"The secret for {secretId} has been successfully stored in the vault.");
                    return true;
                }

                throw new Exception($"HTTP error: {res.Result.HttpStatusCode}");
            }

            catch (Exception ex)
            {
                if (ex.Message.Contains("Secrets Manager can't find the specified secret"))
                {
                    _logger.Information(ex, $"The account {secretId} does not exist in the vault; attempting to the create account .");
                    return CreateAwsAccount(secretId, secret);
                }

                _logger.Error(ex, $"Failed to set the secret for {secretId}: {ex.Message}.");
                return false;
            }
        }

        private bool CreateAwsAccount(string name, string secret)
        {
            var createAccountRequest = new CreateSecretRequest
            {
                Name = name,
                SecretString = secret
            };

            try
            {
                var res = Task.Run(async () => await _awsClient.CreateSecretAsync(createAccountRequest));

                if (res.Result.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.Information($"The secret for {name} has been successfully stored in the vault.");
                    return true;
                }

                throw new Exception($"Http Status Code {res.Result.HttpStatusCode}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to set the secret for {name}: {ex.Message}.");
                return false;
            }
        }

        public void SetPluginConfiguration(Dictionary<string, string> configuration)
        {
            if (configuration != null && 
                configuration.ContainsKey(AccessKeyId) &&
                configuration.ContainsKey(AwsRegion))
            {
                _configuration = configuration;
                _rgx = new Regex("[^a-zA-Z0-9-]");
                _logger.Information($"Plugin {Name} has been successfully configured.");
            }
            else
            {
                _logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            if (_configuration != null)
            {
                try
                {
                    _logger.Information($"Configuring plugin {Name} for the AWS vault");

                    var region = Amazon.RegionEndpoint.GetBySystemName(_configuration[AwsRegion]);

                    if (region.DisplayName == "Unknown")
                        throw new Exception("Specified region is unknown.");

                    var accessKeyId = _configuration[AccessKeyId];

                    //this client doesn't create a connection until it is used. 
                    _awsClient = new AmazonSecretsManagerClient(accessKeyId, credential, region);

                    _logger.Information($"Plugin {Name} has been successfully configured for the AWS vault.");
                }
                catch(Exception ex)
                {
                    _logger.Error(ex, $"Plugin configuration failed: {ex.Message}");
                }
            }
            else
            {
                _logger.Error("The plugin is missing the configuration.");
            }
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public bool TestVaultConnection()
        {
            if (_awsClient == null)
                return false;

            try
            {
                var listRequest = new ListSecretsRequest()
                {
                    MaxResults = 1
                };
                var task = Task.Run(async () => await _awsClient.ListSecretsAsync(listRequest));
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

        public void Unload()
        {
        }
    }
}
