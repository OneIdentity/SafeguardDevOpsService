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
using System.Net.Sockets;


namespace OneIdentity.DevOps.AwsSecretsManagerVault
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private AmazonSecretsManagerClient _awsClient;
        private Dictionary<string, string> _configuration;
        private Regex _rgx;

        private const string AccessKeyId = "accessKeyId";
        private const string AwsRegion = "awsRegion";
        private string FormatAccountName(string altAccountName, string asset, string account) => _rgx.Replace(altAccountName ?? $"{asset}-{account}", "-");

        public string Name => "AwsSecretsManagerVault";
        public string DisplayName => "AWS Secrets Manager Vault";
        public string Description => "This is the AWS Secrets Manager Vault plugin for updating passwords";
        public bool SupportsReverseFlow => true;
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};

        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

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

        public void SetPluginConfiguration(Dictionary<string, string> configuration)
        {
            if (configuration != null && 
                configuration.ContainsKey(AccessKeyId) &&
                configuration.ContainsKey(AwsRegion))
            {
                _configuration = configuration;
                _rgx = new Regex("[^a-zA-Z0-9-]");
                Logger.Information($"Plugin {Name} has been successfully configured.");
            }
            else
            {
                Logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            if (_configuration != null)
            {
                try
                {
                    Logger.Information($"Configuring plugin {Name} for the AWS vault");

                    var region = Amazon.RegionEndpoint.GetBySystemName(_configuration[AwsRegion]);

                    if (region.DisplayName == "Unknown")
                        throw new Exception("Specified region is unknown.");

                    var accessKeyId = _configuration[AccessKeyId];

                    //this client doesn't create a connection until it is used. 
                    _awsClient = new AmazonSecretsManagerClient(accessKeyId, credential, region);

                    Logger.Information($"Plugin {Name} has been successfully configured for the AWS vault.");
                }
                catch(Exception ex)
                {
                    Logger.Error(ex, $"Plugin configuration failed: {ex.Message}");
                }
            }
            else
            {
                Logger.Error("The plugin is missing the configuration.");
            }
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
        }

        private string GetPassword(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            return FetchCredential(FormatAccountName(altAccountName, asset, account));
        }

        private string GetSshKey(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            return FetchCredential(FormatAccountName(altAccountName, asset, account)+"-sshkey");
        }

        private string SetPassword(string asset, string account, string[] password, string altAccountName)
        {
            if (!ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            if (_awsClient == null || !ConfigurationIsValid)
            {
                Logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return null;
            }

            if (password is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            return StoreCredential(FormatAccountName(altAccountName, asset, account), password[0]) ? password[0] : null;
        }

        private string SetSshKey(string asset, string account, string[] sshKey, string altAccountName)
        {
            if (!ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            if (_awsClient == null || !ConfigurationIsValid)
            {
                Logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return null;
            }

            if (sshKey is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            return StoreCredential(FormatAccountName(altAccountName, asset, account)+"-sshkey", sshKey[0]) ? sshKey[0] : null;
        }

        private string SetApiKey(string asset, string account, string[] apiKeys, string altAccountName)
        {
            if (!ValidationHelper.CanHandleApiKey(this))
            {
                return null;
            }

            if (_awsClient == null || !ConfigurationIsValid)
            {
                Logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return null;
            }

            var name = FormatAccountName(altAccountName, asset, account);
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
                    Logger.Error($"The ApiKey {name} {apiKey.ClientId} failed to save to the {DisplayName} vault.");
                    retval = false;
                }
            }

            return retval ? "" : null;
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
                    Logger.Information($"The secret for {secretId} has been successfully stored in the vault.");
                    return true;
                }

                throw new Exception($"HTTP error: {res.Result.HttpStatusCode}");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Secrets Manager can't find the specified secret"))
                {
                    Logger.Information($"The account {secretId} does not exist in the vault; attempting to the create account .");
                    return CreateAwsAccount(secretId, secret);
                }

                Logger.Error(ex, $"Failed to set the secret for {secretId}: {ex.Message}.");
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
                    Logger.Information($"The secret for {name} has been successfully stored in the vault.");
                    return true;
                }

                throw new Exception($"Http Status Code {res.Result.HttpStatusCode}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set the secret for {name}: {ex.Message}.");
                return false;
            }
        }

        private string FetchCredential(string secretId)
        {
            try
            {
                var request = new GetSecretValueRequest()
                {
                    SecretId = secretId
                };

                var res = Task.Run(async () => await _awsClient.GetSecretValueAsync(request));

                if (res.Result.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    Logger.Information($"The secret for {secretId} has been fetched from the {DisplayName} vault.");
                    return res.Result.SecretString;
                }

                Logger.Error($"Failed to fetch the secret for {secretId} in the vault {DisplayName}.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to fetch the secret for {secretId} in the vault {DisplayName}: {ex.Message}.");
            }

            return null;
        }
    }
}
