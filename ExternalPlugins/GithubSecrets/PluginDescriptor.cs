using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octokit;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.GithubSecrets
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private GitHubClient _secretsClient;
        private Dictionary<string,string> _configuration;
        private Regex _rgx;
        private Repository _repository;
        private SecretsPublicKey _publicKey; 

        private const string RepositoryName = "Repository Name";

        public string Name => "GithubSecrets";
        public string DisplayName => "Github Secrets";
        public string Description => "This is the Github Secrets plugin for updating passwords";
        public bool SupportsReverseFlow => false;

        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false; //Github only stores secrets that can be used in an action. The secret cannot be fetched.
        public ILogger Logger { get; set; }

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { RepositoryName, "" }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(RepositoryName))
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
            if (_configuration != null && _configuration.ContainsKey(RepositoryName) && !string.IsNullOrEmpty(_configuration[RepositoryName]))
            {
                _secretsClient = new GitHubClient(new ProductHeaderValue(_configuration[RepositoryName]));
                var tokenAuth = new Credentials(credential);
                _secretsClient.Credentials = tokenAuth;

                try
                {
                    var result = Task.Run(async () => await _secretsClient.Repository.GetAllForCurrent());
                    var repos = result.Result.ToList();
                    var repo = repos.FirstOrDefault(x =>
                        x.Name.Equals(_configuration[RepositoryName], StringComparison.OrdinalIgnoreCase));
                    if (repo != null)
                    {
                        _repository = repo;
                        Logger.Information(
                            $"Plugin {Name} has been successfully authenticated to the Github environment.");

                        var pKeyResult = Task.Run(async () =>
                            await _secretsClient.Repository.Actions.Secrets.GetPublicKey(_repository.Owner.Login,
                                _repository.Name));
                        _publicKey = pKeyResult.Result;

                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,
                        $"Failed to get the repository information for {_configuration[RepositoryName]}: {ex.Message}.");
                }

                Logger.Information($"Plugin {Name} failed to authenticated to the Github environment. Repository {_configuration[RepositoryName]} not found.");
            }
            else
            {
                Logger.Error("The plugin is missing the configuration.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_secretsClient == null)
                return false;

            if (_repository != null)
            {
                Logger.Information($"Test connection for {DisplayName}: Result = {_repository.Name}");
                return true;
            }
            Logger.Error($"Failed the connection test for {DisplayName}.");
            return false;
        }

        public string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName)
        {
            ValidationHelper.CanReverseFlow(this);
            return null;
        }

        public string SetCredential(CredentialType credentialType, string asset, string account, string[] credential, string altAccountName)
        {
            switch (credentialType)
            {
                case CredentialType.Password:
                    return SetPassword(asset, account, credential, altAccountName);
                case CredentialType.SshKey:
                    ValidationHelper.CanHandleSshKey(this);
                    break;
                case CredentialType.ApiKey:
                    ValidationHelper.CanHandleApiKey(this);
                    break;
                default:
                    Logger.Error($"Invalid credential type sent to the {DisplayName} plugin instance.");
                    break;
            }

            return null;
        }
        
        public void Unload()
        {
        }

        private string SetPassword(string asset, string account, string[] password, string altAccountName)
        {
            if (!ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            if (_secretsClient == null || _configuration == null || _publicKey == null)
            {
                Logger.Error($"No connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            if (password is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            try
            {
                var name = _rgx.Replace(altAccountName ?? $"{asset}_{account}", "_");

                // The password has to be encrypted prior to storing it in the Github repository.
                // The public key id that is associated with the encryption must also be included.
                var sealedPublicKeyBox = Sodium.SealedPublicKeyBox.Create(System.Text.Encoding.UTF8.GetBytes(password[0]), Convert.FromBase64String(_publicKey.Key));
                var upsertSecrets = new UpsertRepositorySecret()
                {
                    EncryptedValue = Convert.ToBase64String(sealedPublicKeyBox),
                    KeyId = _publicKey.KeyId
                };

                var secret = Task.Run(async () => await _secretsClient.Repository.Actions.Secrets.CreateOrUpdate(_repository.Owner.Login, _repository.Name, name, upsertSecrets)).Result;
                Logger.Information($"Password for {name} has been successfully stored in the {_repository.Name} action secrets.");

                return password[0];
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to set the secret for {asset}-{altAccountName ?? account}: {ex.Message}.");
                return null;
            }
        }
    }
}
