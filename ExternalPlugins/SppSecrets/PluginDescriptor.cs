using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using OneIdentity.DevOps.Common;
using OneIdentity.SafeguardDotNet;
using Serilog;

namespace OneIdentity.DevOps.SppSecrets
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private ISafeguardConnection _sppConnection;
        private Dictionary<string,string> _configuration;
        private A2ARegistration _a2aRegistration;
        private AccountGroup _accountGroup;
        private ILogger _logger;

        private const string SppAppliance = "Spp Appliance";
        private const string SppUser = "Spp User";
        private const string SppA2aRegistrationName = "Spp A2A Registration Name";
        private const string SppA2aCertificateUser = "Spp A2A Certificate User";
        private const string SppAccountGroup = "Spp Account Group";

        public string Name => "SppSecrets";
        public string DisplayName => "Safeguard for Privileged Passwords Secrets";
        public string Description => "This is the Safeguard for Privileged Passwords Secrets plugin for updating passwords";
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { SppAppliance, "" },
                { SppUser, "" },
                { SppA2aRegistrationName, "" },
                { SppA2aCertificateUser, "" },
                { SppAccountGroup, "" }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(SppUser) && configuration.ContainsKey(SppAppliance) &&
                configuration.ContainsKey(SppA2aCertificateUser) && configuration.ContainsKey(SppA2aRegistrationName))
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
                _sppConnection = Safeguard.Connect(_configuration[SppAppliance], "local", _configuration[SppUser], credential.ToSecureString(), ignoreSsl: true);
                _logger.Information($"Plugin {Name} has been successfully authenticated to the Azure vault.");

                if (!CheckOrAddExternalA2aRegistration())
                {
                    _logger.Error($"Failed to find or add the A2A registration {_configuration[SppA2aRegistrationName]}.");
                }
            }
            else
            {
                _logger.Error("The plugin is missing the configuration.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_sppConnection == null)
                return false;

            try
            {
                var meResult = _sppConnection.InvokeMethodFull(Service.Core, Method.Get, "/Me");
                var me = JsonHelper.DeserializeObject<User>(meResult.Body);
                _logger.Information($"Test vault connection for {me.DisplayName}");
                if (me.Disabled || me.Locked)
                {
                    _logger.Error($"Failed the connection test for {DisplayName}. The specified user is unavailable.");
                    return false;
                }
                if (!me.AdminRoles.Contains("PolicyAdmin"))
                {
                    _logger.Error($"Failed the connection test for {DisplayName}. The specified user must be an SPP Policy Administrator.");
                    return false;
                }

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
            if (AssignedCredentialType != CredentialType.Password)
            {
                _logger.Error("This plugin instance does not handle the Password credential type.");
                return false;
            }

            if (_sppConnection == null || _configuration == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            return StoreCredential(asset, altAccountName ?? account, password);
        }

        public bool SetSshKey(string asset, string account, string sshKey, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.SshKey)
            {
                _logger.Error("This plugin instance does not handle the SshKey credential type.");
                return false;
            }

            if (_sppConnection == null || _configuration == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            return StoreCredential(asset, altAccountName ?? account, sshKey);
        }

        public bool SetApiKey(string asset, string account, string[] apiKeys, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.ApiKey)
            {
                _logger.Error("This plugin instance does not handle the ApiKey credential type.");
                return false;
            }

            if (_sppConnection == null || _configuration == null)
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var retval = true;
            foreach (var apiKeyJson in apiKeys)
            {
                if (!StoreCredential(asset, altAccountName ?? account, apiKeyJson))
                {
                    retval = false;
                }
            }

            return retval;
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Unload()
        {
        }

        private bool StoreCredential(string assetName, string accountName, string payload = null)
        {
            try
            {
                var asset = GetAsset(assetName);
                if (asset == null)
                {
                    asset = CreateAsset(assetName);
                    if (asset == null)
                    {
                        _logger.Information($"Failed to store the secret due to a failure to create an asset for {assetName}.");
                        return false;
                    }
                }

                var account = GetAccount(asset, accountName);
                if (account == null)
                {
                    account = CreateAccount(asset, accountName);
                    if (account == null)
                    {
                        _logger.Information($"Failed to store the secret due to a failure to create an account for {accountName}.");
                        return false;
                    }
                }

                if (_accountGroup != null)
                {
                    if (!_accountGroup.Accounts.Any(x => x.Name.Equals(account.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        AddAccountToGroup(account);
                    }
                }

                switch (AssignedCredentialType)
                {
                    case CredentialType.Password:
                        SaveAccountPassword(account, payload);
                        break;
                    case CredentialType.SshKey:
                        SaveAccountSshKey(account, payload);
                        break;
                    case CredentialType.ApiKey:
                        SaveAccountApiKey(account, payload);
                        break;
                }
                _logger.Information($"The secret for {assetName}-{accountName} has been successfully stored in the vault.");

                // Look up A2A Registration and create one if needed.
                if (!CheckOrAddExternalA2aRegistration())
                {
                    _logger.Error($"Failed to add the secret to the A2A registration {_configuration[SppA2aRegistrationName]}.");
                    return false;
                }

                if (!AddA2ARetrievableAccount(account))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to set the secret for {assetName}-{accountName}: {ex.Message}.");
                return false;
            }
        }

        private void SaveAccountPassword(Account account, string password)
        {
            if (_sppConnection == null)
                return;
        
            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Put, $"AssetAccounts/{account.Id}/Password", $"\"{password}\"");
                if (result.StatusCode != HttpStatusCode.NoContent)
                {
                    _logger.Error(
                        $"Failed to save the password for asset {account.Asset.Name} account {account.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex,
                    $"Failed to save the password for asset {account.Asset.Name} account {account.Name}: {ex.Message}");
            }
        }

        private void SaveAccountSshKey(Account account, string sshKey)
        {
            if (_sppConnection == null)
                return;

            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Put, $"AssetAccounts/{account.Id}/SshKey", $"{{\"PrivateKey\":\"{sshKey.ReplaceLineEndings(string.Empty)}\"}}");
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Error(
                        $"Failed to save the SSH key for asset {account.Asset.Name} account {account.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex,
                    $"Failed to save the SSH key for asset {account.Asset.Name} account {account.Name}: {ex.Message}");
            }
        }

        private void SaveAccountApiKey(Account account, string apiKeyJson)
        {
            if (_sppConnection == null)
                return;

            try
            {
                var apiKey = JsonHelper.DeserializeObject<ApiKey>(apiKeyJson);
                if (apiKey != null)
                {
                    if (!HasApiKey(account, apiKey))
                    {
                        apiKey = CreateApiKey(account, apiKey);
                        if (apiKey == null)
                        {
                            _logger.Information($"Failed to store the API key secret due to a failure to create the API key for the account {account.Name}.");
                            return;
                        }
                    }

                    var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Put,
                        $"AssetAccounts/{account.Id}/ApiKeys/{apiKey.ClientSecret}/ClientSecret", apiKeyJson);
                    if (result.StatusCode != HttpStatusCode.NoContent)
                    {
                        _logger.Error(
                            $"Failed to save the API key secret for account {account.Name} API key {apiKey.Name} to {DisplayName}.");
                    }
                }
                else
                {
                    _logger.Error($"The ApiKey {apiKey.Name} failed to save to {DisplayName}.");
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex,
                    $"Failed to save the Api key for account {account.Name} to {DisplayName}: {ex.Message}");
            }
        }
        
        private bool CheckOrAddExternalA2aRegistration()
        {
            _a2aRegistration = GetA2ARegistration();

            // If the a2a registration is not found by name, then create one.
            if (_a2aRegistration == null)
            {
                _a2aRegistration = CreateA2ARegistration();
                if (_a2aRegistration == null )
                    return false; 
            }

            if (!string.IsNullOrEmpty(_configuration[SppAccountGroup]))
            {
                _accountGroup = GetAccountGroup();

                // If the account group is not found by name, then create one.
                if (_accountGroup == null)
                {
                    _accountGroup = CreateAccountGroup();
                }
            }

            return true;
        }

        private A2ARegistration CreateA2ARegistration()
        {
            if (_sppConnection == null)
                return null;

            var a2aUser = GetA2AUser();

            if (a2aUser == null)
            {
                _logger.Error($"Failed to create the A2A registration for {_configuration[SppA2aRegistrationName]}. The A2A certificate user is missing and needs to be created.");
                return null;
            }

            var registration = new A2ARegistration()
            {
                AppName = _configuration[SppA2aRegistrationName],
                CertificateUserId = a2aUser.Id,
                VisibleToCertificateUsers = true
            };

            var registrationStr = JsonHelper.SerializeObject(registration);

            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Post, "A2ARegistrations", registrationStr);
                if (result.StatusCode == HttpStatusCode.Created)
                {
                    registration = JsonHelper.DeserializeObject<A2ARegistration>(result.Body);
                    return registration;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to create the A2A registration for {_configuration[SppA2aRegistrationName]}: {ex.Message}.");
            }

            return null;
        }

        private A2ARegistration GetA2ARegistration()
        {
            if (_sppConnection == null)
                return null;

            try
            {
                var p = new Dictionary<string, string>
                    {{"filter", $"AppName eq '{_configuration[SppA2aRegistrationName]}'"}};

                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Get, "A2ARegistrations", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var foundRegistrations = JsonHelper.DeserializeObject<List<A2ARegistration>>(result.Body);

                    if (foundRegistrations.Count > 0)
                    {
                        var registration = foundRegistrations.FirstOrDefault();
                        return registration;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get the A2A registration by name {SppA2aRegistrationName}: {ex.Message}");
            }

            return null;
        }

        private User GetA2AUser()
        {
            if (_sppConnection == null)
                return null;

            var p = new Dictionary<string, string>
                {{"filter", $"Name eq '{_configuration[SppA2aCertificateUser]}'"}};

            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Get, "Users", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var foundUsers = JsonHelper.DeserializeObject<List<User>>(result.Body);

                    if (foundUsers.Count > 0)
                    {
                        var a2aUser = foundUsers.FirstOrDefault();
                        return a2aUser;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get the A2A user by name: {ex.Message}");
            }

            return null;
        }

        private Asset CreateAsset(string assetName)
        {
            if (_sppConnection == null)
                return null;

            var asset = new Asset()
            {
                Id = 0,
                Name =  assetName,
                Description = "Asset created by DevOps Secrets Broker",
                PlatformId = 500, //Other
                AssetPartitionId = -1
            };

            var assetBody = JsonHelper.SerializeObject(asset);
            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Post, "Assets", assetBody);
                if (result.StatusCode == HttpStatusCode.Created)
                {
                    asset = JsonHelper.DeserializeObject<Asset>(result.Body);
                    return asset;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to create an asset for {assetName}: {ex.Message}");
            }

            return null;
        }

        private Asset GetAsset(string assetName)
        {
            if (_sppConnection == null)
                return null;

            try
            {
                var p = new Dictionary<string, string>
                    {{"filter", $"Name eq '{assetName}'"}};

                FullResponse result = _sppConnection.InvokeMethodFull(Service.Core, Method.Get, "Assets", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var foundAssets = JsonHelper.DeserializeObject<List<Asset>>(result.Body);

                    if (foundAssets.Count > 0)
                    {
                        var asset = foundAssets.FirstOrDefault();
                        return asset;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get the asset by name {assetName}: {ex.Message}");
            }

            return null;
        }

        private Account CreateAccount(Asset asset, string accountName)
        {
            if (_sppConnection == null)
                return null;
        
            var account = new Account()
            {
                Name =  accountName,
                Description = "Account created by DevOps Secrets Broker",
                Asset = new Asset()
                {
                    Id = asset.Id
                }
            };

            var accountBody = JsonHelper.SerializeObject(account);
            
            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Post, "AssetAccounts", accountBody);
                if (result.StatusCode == HttpStatusCode.Created)
                {
                    account = JsonHelper.DeserializeObject<Account>(result.Body);
                    _logger.Information($"Successfully added asset account {account.Name} to safeguard.");
                    return account;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to add the account {account.Name} to safeguard for '{asset.Name}': {ex.Message}");
            }
            
            return null;
        }

        private Account GetAccount(Asset asset, string accountName)
        {
            if (_sppConnection == null)
                return null;

            try
            {
                var p = new Dictionary<string, string>
                    {{"filter", $"Name eq '{accountName}'"}};

                FullResponse result = _sppConnection.InvokeMethodFull(Service.Core, Method.Get, $"Assets/{asset.Id}/Accounts", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var foundAccounts = JsonHelper.DeserializeObject<List<Account>>(result.Body);

                    if (foundAccounts.Count > 0)
                    {
                        var account = foundAccounts.FirstOrDefault();
                        return account;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get the account by name for asset {asset.Name} account {accountName}: {ex.Message}");
            }

            return null;
        }

        private AccountGroup CreateAccountGroup()
        {
            if (_sppConnection == null)
                return null;

            var accountGroup = new AccountGroup()
            {
                Id = 0,
                Name =  _configuration[SppAccountGroup],
                Description = "Account group created by DevOps Secrets Broker",
            };

            var accountGroupBody = JsonHelper.SerializeObject(accountGroup);
            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Post, "AccountGroups", accountGroupBody);
                if (result.StatusCode == HttpStatusCode.Created)
                {
                    accountGroup = JsonHelper.DeserializeObject<AccountGroup>(result.Body);
                    return accountGroup;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to create an asset for {_configuration[SppAccountGroup]}: {ex.Message}");
            }

            return null;
        }

        private AccountGroup GetAccountGroup()
        {
            if (_sppConnection == null)
                return null;

            try
            {
                var p = new Dictionary<string, string>
                    {{"filter", $"Name eq '{_configuration[SppAccountGroup]}'"}};

                FullResponse result = _sppConnection.InvokeMethodFull(Service.Core, Method.Get, "AccountGroups", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var foundAccountGroups = JsonHelper.DeserializeObject<List<AccountGroup>>(result.Body);

                    if (foundAccountGroups.Count > 0)
                    {
                        var asset = foundAccountGroups.FirstOrDefault();
                        return asset;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get the asset by name {_configuration[SppAccountGroup]}: {ex.Message}");
            }

            return null;
        }

        private bool AddAccountToGroup(Account account)
        {
            if (_sppConnection == null)
                return false;

            var accountBody = JsonHelper.SerializeObject(new[] {account});
            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Post, $"AccountGroups/{_accountGroup.Id}/Accounts/Add", accountBody);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to add the account {account.Name} to account group {_accountGroup.Name}: {ex.Message}");
            }

            return false;
        }

        private ApiKey CreateApiKey(Account account, ApiKey apiKey)
        {
            if (_sppConnection == null)
                return null;
        
            var newApiKey = new ApiKey()
            {
                Name =  apiKey.Name,
                Description = apiKey.Description,
            };

            var apiKeyBody = JsonHelper.SerializeObject(newApiKey);
            
            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Post, $"AssetAccounts/{account.Id}/ApiKeys", apiKeyBody);
                if (result.StatusCode == HttpStatusCode.Created)
                {
                    newApiKey = JsonHelper.DeserializeObject<ApiKey>(result.Body);
                    _logger.Information($"Successfully added API key {newApiKey.Name} to account {account.Name}.");
                    return newApiKey;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to add the account {account.Name} to safeguard': {ex.Message}");
            }
            
            return null;
        }

        private bool HasApiKey(Account account, ApiKey apiKey)
        {
            if (_sppConnection == null)
                return false;

            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Get, $"AssetAccounts/{account.Id}/ApiKeys/{apiKey.Id}");
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to check the API key for the account {account.Name}: {ex.Message}");
            }

            return false;
        }

        private bool AddA2ARetrievableAccount(Account account)
        {
            if (_sppConnection == null)
                return false;

            if (_a2aRegistration == null)
            {
                _logger.Error(
                    $"Failed to add the A2A registration for asset {account.Asset.Name} account {account.Name}. The A2A registration {_configuration[SppA2aRegistrationName]} does not exist.");
                return false;
            }

            try
            {
                var result = _sppConnection.InvokeMethodFull(Service.Core, Method.Post,
                    $"A2ARegistrations/{_a2aRegistration.Id}/RetrievableAccounts",
                    $"{{\"AccountId\":{account.Id}, \"IpRestrictions\":[]}}");
                if (result.StatusCode != HttpStatusCode.OK && result.StatusCode != HttpStatusCode.Created)
                {
                    _logger.Error(
                        $"Failed to add account {account.Id} - {account.Name} to the A2A registration {_a2aRegistration.AppName}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to add account {account.Id} - {account.Name} to the A2A registration {_a2aRegistration.AppName}: {ex.Message}");
                return false;
            }

            _logger.Information($"Successfully added account {account.Name} to A2A registration {_a2aRegistration.AppName}.");
            return true;
        }
    }

    class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public List<string> AdminRoles { get; set; }
        public bool Disabled { get; set; }
        public bool Locked { get; set; }
    }

    public class A2ARegistration
    {
        public int Id { get; set; }
        public string AppName { get; set; }
        public string Description { get; set; }
        public string DevOpsInstanceId { get; set; }
        public bool Disabled { get; set; }
        public bool VisibleToCertificateUsers { get; set; }
        public int CertificateUserId { get; set; }
        public string CertificateUserThumbPrint { get; set; }
        public string CertificateUser { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByUserDisplayName { get; set; }
    }

    public class Asset
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string NetworkAddress { get; set; }
        public int PlatformId { get; set; }
        public string PlatformDisplayName { get; set; }
        public int AssetPartitionId { get; set; }
        public string AssetPartitionName { get; set; }
    }

    public class Account
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DomainName { get; set; }
        public bool HasPassword { get; set; }
        public bool HasSshKey { get; set; }
        public bool HasApiKey { get; set; }
        public bool Disabled { get; set; }
        public Asset Asset { get; set; }
    }

    public class AccountGroup
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Account[] Accounts { get; set; }
    }

}
