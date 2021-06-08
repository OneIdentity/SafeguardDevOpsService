using System;
using System.Collections.Generic;
using OneIdentity.DevOps.Common;
using Serilog;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Threading.Tasks;


namespace OneIdentity.DevOps.AwsSecretsManagerVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static AmazonSecretsManagerClient _awsClient;
        private static Dictionary<string, string> _configuration;
        private static ILogger _logger;

        private const string AccessKeyId = "accessKeyId";
        private const string AwsRegion = "awsRegion";
        
        public string Name => "AwsSecretsManagerVault";
        public string DisplayName => "AWS Secrets Manager Vault";
        public string Description => "This is the AWS Secrets Manager Vault plugin for updating passwords";

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

        public bool SetPassword(string asset, string account, string password)
        {
            if (_awsClient == null || !ConfigurationIsValid)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            try
            {
                var name = $"{asset}-{account}";
              
                var request = new PutSecretValueRequest()
                {
                    SecretId = name,
                    SecretString = password
                };

                var res = Task.Run(async () => await _awsClient.PutSecretValueAsync(request));

                if (res.Result.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.Information($"Successfully set the secret for {name}");
                    return true;
                }
                else
                    throw new Exception($"HTTP error: {res.Result.HttpStatusCode}");
            }

            catch (Exception ex)
            {
                if (ex.Message.Contains("Secrets Manager can't find the specified secret"))
                {
                    _logger.Information($"Account does not exist in vault; attempting to create account.");
                    return CreateAwsAccount(asset, account, password);
                }
                else
                {
                    _logger.Error($"Failed to set the secret for {asset}-{account}: {ex.Message}.");
                    return false;
                }
            }
        }

        public void SetPluginConfiguration(Dictionary<string, string> configuration)
        {
            if (configuration != null && 
                configuration.ContainsKey(AccessKeyId) &&
                configuration.ContainsKey(AwsRegion))
            {
                _configuration = configuration;
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
                    _logger.Error($"Plugin configuration failed: {ex.Message}");
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
                _logger.Error($"Failed the connection test for {DisplayName}: {ex.Message}.");
                return false;
            }
        }

        public void Unload()
        {
        }

        private bool CreateAwsAccount(string asset, string account, string password)
        {
            if (_awsClient == null || !ConfigurationIsValid)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = $"{asset}-{account}";

            try
            {
                var createAccountRequest = new CreateSecretRequest
                {
                    Name = name,
                    SecretString = password
                };

                var res = Task.Run(async () => await _awsClient.CreateSecretAsync(createAccountRequest));

                if (res.Result.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.Information($"Successfully created account {name} in vault.");
                    return true;
                }
                else
                {
                    throw new Exception($"Http Status Code {res.Result.HttpStatusCode}");
                }
            }
            catch (Exception createEx)
            {
                _logger.Error($"Failed to create account {name} in vault. Message: {createEx.Message}");
                return false;
            }
        }
    }
}
