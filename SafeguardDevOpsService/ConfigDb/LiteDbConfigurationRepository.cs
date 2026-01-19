using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CredentialManagement;
using LiteDB;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.DevOps.Extensions;
using OneIdentity.DevOps.Logic;
using CredentialType = CredentialManagement.CredentialType;

namespace OneIdentity.DevOps.ConfigDb
{
    internal class LiteDbConfigurationRepository : IConfigurationRepository, IDisposable
    {
        private bool _disposed;
        private readonly Serilog.ILogger _logger;
        private LiteDatabase _configurationDb;
        private X509Certificate2Collection _trustedCertificateCollection;
        private ILiteCollection<Setting> _settings;
        private ILiteCollection<AccountMapping> _accountMappings;
        private ILiteCollection<Plugin> _plugins;
        private ILiteCollection<Addon> _addons;
        private ILiteCollection<TrustedCertificate> _trustedCertificates;
        private string _svcId;

        private const string SettingsTableName = "settings";
        private const string AccountMappingsTableName = "accountmappings";
        private const string PluginsTableName = "plugins";
        private const string AddonsTableName = "addons";
        private const string TrustedCertificateTableName = "trustedcertificates";

        private const string SafeguardAddressKey = "SafeguardAddress";
        private const string ApiVersionKey = "ApiVersion";
        private const string IgnoreSslKey = "IgnoreSsl";
        private const string A2aUserIdKey = "A2aUserId";
        private const string A2aRegistrationIdKey = "A2aRegistrationId";
        private const string A2aVaultRegistrationIdKey = "A2aVaultRegistrationId";
        private const string AssetIdKey = "AssetId";
        private const string AssetPartitionIdKey = "AssetPartitionId";
        private const string AssetAccountGroupIdKey = "AssetAccountGroupId";
        private const string SigningCertificateKey = "SigningCertificate";
        private const string LastKnownMonitorStateKey = "LastKnownMonitorState";
        private const string LastKnownReverseFlowMonitorStateKey = "LastKnownReverseFlowMonitorState";
        private const string ReverseFlowPollingIntervalKey = "ReverseFlowPollingInterval";

        private const string UserCertificateThumbprintKey = "UserCertThumbprint";
        private const string UserCertificateDataKey = "UserCertData";
        private const string UserCertificatePassphraseKey = "UserCertPassphrase";
        private const string UserCsrDataKey = "UserCertificateSigningRequestData";
        private const string UserCsrPrivateKeyDataKey = "UserCertificateSigningRequestPrivateKeyData";
        private const string WebSslCertificateDataKey = "WebSslCertData";
        private const string WebSslCertificatePassphraseKey = "WebSslCertPassphrase";
        private const string WebSslCsrDataKey = "WebSslCertificateSigningRequestData";
        private const string WebSslCsrPrivateKeyDataKey = "WebSslCertificateSigningRequestPrivateKeyData";

        private readonly bool _isLinux;

        public LiteDbConfigurationRepository()
        {
            _logger = Serilog.Log.Logger;
            _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!Directory.Exists(WellKnownData.ProgramDataPath))
                Directory.CreateDirectory(WellKnownData.ProgramDataPath);
            var dbPath = Path.Combine(WellKnownData.ProgramDataPath, WellKnownData.DbFileName);
            _logger.Information($"Loading configuration database at {dbPath}.");

            var passwd = GetPassword();
            if (string.IsNullOrEmpty(passwd) && !_isLinux)
            {
                passwd = SavePassword(GeneratePassword());
            }

            if (!string.IsNullOrEmpty(passwd))
            {
                _logger.Information("The database is encrypted.");
            }

            var connectionString = $"Filename={dbPath}";
            connectionString += string.IsNullOrEmpty(passwd) ? "" : $";Password={passwd}";
            _configurationDb = new LiteDatabase(connectionString);
            _disposed = false;
            _settings = _configurationDb.GetCollection<Setting>(SettingsTableName);
            _accountMappings = _configurationDb.GetCollection<AccountMapping>(AccountMappingsTableName);
            _plugins = _configurationDb.GetCollection<Plugin>(PluginsTableName);
            _addons = _configurationDb.GetCollection<Addon>(AddonsTableName);
            _trustedCertificates = _configurationDb.GetCollection<TrustedCertificate>(TrustedCertificateTableName);
        }

        private string GeneratePassword()
        {
            var random = new byte[24];
            var rng = RandomNumberGenerator.Create();
            rng.GetNonZeroBytes(random);
            return Convert.ToBase64String(random);
        }

        public string SavePassword(string password)
        {
            if (_isLinux)
            {
                var curPassword = GetPassword();
                if (string.IsNullOrEmpty(curPassword) || !curPassword.Equals(password))
                {
                    Environment.SetEnvironmentVariable(WellKnownData.CredentialEnvVar, password);
                    return password;
                }

                return curPassword;
            }

            try
            {
                using (var cred = new Credential())
                {
                    cred.Password = password;
                    cred.Target = WellKnownData.CredentialTarget;
                    cred.Type = CredentialType.Generic;
                    cred.PersistanceType = PersistanceType.LocalComputer;
                    cred.Save();

                    return password;
                }
            }
            catch (Exception ex)
            {
                var msg = $"Failed to get the credential needed to open the database. {ex.Message}";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }
        }

        private string GetPassword()
        {
            if (_isLinux)
            {
                return Environment.GetEnvironmentVariable(WellKnownData.CredentialEnvVar);
            }

            using (var cred = new Credential())
            {
                cred.Target = WellKnownData.CredentialTarget;
                cred.Load();
                return cred.Password;
            }
        }

        private void DeletePassword()
        {
            if (_isLinux)
            {
                return;
            }

            using (var cred = new Credential())
            {
                cred.Target = WellKnownData.CredentialTarget;
                cred.Delete();
            }
        }

        private string GetSimpleSetting(string name)
        {
            var obj = GetSetting(name);
            return obj?.Value;
        }

        private void SetSimpleSetting(string name, string value)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
            var obj = new Setting()
            {
                Name = name,
                Value = value ?? ""
            };
            SetSetting(obj);
        }

        public ISetting GetSetting(string name)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
            return _settings.FindById(name);
        }

        public void SetSetting(ISetting value)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
            _settings.Upsert((Setting)value);
        }

        public void RemoveSetting(string name)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
            _settings.Delete(name);
        }

        public IEnumerable<Plugin> GetAllPlugins()
        {
            return _plugins.FindAll();
        }

        public Plugin GetPluginByName(string name)
        {
            return _plugins.FindById(name);
        }

        public IEnumerable<Plugin> GetPluginInstancesByName(string name)
        {
            var instances = _plugins.FindAll();
            return instances.Where(x => x.Name.StartsWith(name));
        }

        public IEnumerable<Plugin> GetAllReverseFlowPluginInstances()
        {
            var instances = _plugins.FindAll();
            return instances.Where(x => x.ReverseFlowEnabled);
        }

        public Plugin SavePluginConfiguration(Plugin plugin)
        {
            _plugins.Upsert(plugin);
            return plugin;
        }

        public bool DeletePluginByName(string name, bool hardDelete = false)
        {
            if (hardDelete)
            {
                return _plugins.Delete(name);
            }

            var plugin = GetPluginByName(name);
            if (plugin != null)
            {
                plugin.IsDeleted = true;
                SavePluginConfiguration(plugin);
            }

            return true;
        }

        public Plugin SetRootPlugin(string name, bool isRoot)
        {
            var plugin = GetPluginByName(name);
            if (plugin != null && plugin.IsRootPlugin != isRoot)
            {
                plugin.IsRootPlugin = isRoot;
                SavePluginConfiguration(plugin);
            }

            return plugin;
        }

        public IEnumerable<Addon> GetAllAddons()
        {
            return _addons.FindAll();
        }

        public Addon GetAddonByName(string name)
        {
            return _addons.FindById(name);
        }

        public Addon SaveAddon(Addon addon)
        {
            _addons.Upsert(addon);
            return addon;
        }

        public void DeleteAddonByName(string name)
        {
            _addons.Delete(name);
        }

        public IEnumerable<AccountMapping> GetAccountMappings()
        {
            return _accountMappings.FindAll();
        }

        public IEnumerable<AccountMapping> GetAccountMappings(string name)
        {
            if (GetPluginByName(name) == null)
            {
                var msg = $"Plugin {name} not found";
                _logger.Error(msg);
                throw new DevOpsException(msg, HttpStatusCode.NotFound);
            }

            var mappings = GetAccountMappings();

            var accountMappings = mappings.Where(x => x.VaultName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            return accountMappings;
        }


        public void GetAccountMappingsByKey(string key)
        {
            _accountMappings.FindById(key);
        }

        public void SaveAccountMappings(IEnumerable<AccountMapping> accounts)
        {
            foreach (var accountMapping in accounts)
            {
                _accountMappings.Upsert(accountMapping);
            }
        }

        public void DeleteAccountMappingsByKey(string key)
        {
            _accountMappings.Delete(key);
        }

        public void DeleteAccountMappings()
        {
            _accountMappings.DeleteAll();
        }

        public IEnumerable<TrustedCertificate> GetAllTrustedCertificates()
        {
            return _trustedCertificates.FindAll();
        }

        public TrustedCertificate GetTrustedCertificateByThumbPrint(string thumbprint)
        {
            return _trustedCertificates.FindById(thumbprint);
        }

        public TrustedCertificate SaveTrustedCertificate(TrustedCertificate trustedCertificate)
        {
            _trustedCertificateCollection = null;
            _trustedCertificates.Upsert(trustedCertificate);
            return trustedCertificate;
        }

        public void DeleteTrustedCertificateByThumbPrint(string thumbprint)
        {
            _trustedCertificateCollection = null;
            _trustedCertificates.Delete(thumbprint);
        }

        public void DeleteAllTrustedCertificates()
        {
            _trustedCertificateCollection = null;
            _trustedCertificates.DeleteAll();
        }

        public X509Chain GetTrustedChain()
        {
            if (_trustedCertificateCollection == null)
            {
                _trustedCertificateCollection = new X509Certificate2Collection();
                var trustedCertificates = GetAllTrustedCertificates();
                foreach (var trustedCertificate in trustedCertificates)
                {
                    _trustedCertificateCollection.Add(trustedCertificate.GetCertificate());
                }
            }

            var trustedChain = new X509Chain
            {
                ChainPolicy =
                {
                    VerificationFlags = X509VerificationFlags.IgnoreRootRevocationUnknown | X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown | X509VerificationFlags.AllowUnknownCertificateAuthority,
                    RevocationFlag = X509RevocationFlag.EntireChain,
                    RevocationMode = X509RevocationMode.NoCheck
                }
            };
            trustedChain.ChainPolicy.ExtraStore.AddRange(_trustedCertificateCollection);

            return trustedChain;
        }


        public string SafeguardAddress
        {
            get => GetSimpleSetting(SafeguardAddressKey);
            set => SetSimpleSetting(SafeguardAddressKey, value);
        }

        public int? ApiVersion
        {
            get
            {
                try
                {
                    return int.Parse(GetSimpleSetting(ApiVersionKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(ApiVersionKey, value.ToString());
        }

        public string SvcId
        {
            get
            {
                if (_svcId == null) 
                {
                    try
                    {
                        _svcId = File.ReadAllText(WellKnownData.SvcIdPath);
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Failed to read the service instance identifier: {WellKnownData.SvcIdPath}";
                        _logger.Error(msg, ex);
                        throw new DevOpsException(msg, ex);
                    }
                }

                return _svcId;
            }

            set
            {
                _svcId = value;
                File.WriteAllText(WellKnownData.SvcIdPath, value);
            }
        }

        public string DbPasswd => GetPassword();

        public bool? IgnoreSsl
        {
            get
            {
                try
                {
                    return bool.Parse(GetSimpleSetting(IgnoreSslKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(IgnoreSslKey, value.ToString());
        }

        public int? A2aUserId
        {
            get
            {
                try
                {
                    return int.Parse(GetSimpleSetting(A2aUserIdKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(A2aUserIdKey, value.ToString());
        }

        public int? A2aRegistrationId
        {
            get
            {
                try
                {
                    return int.Parse(GetSimpleSetting(A2aRegistrationIdKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(A2aRegistrationIdKey, value.ToString());
        }

        public int? A2aVaultRegistrationId
        {
            get
            {
                try
                {
                    return int.Parse(GetSimpleSetting(A2aVaultRegistrationIdKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(A2aVaultRegistrationIdKey, value.ToString());
        }

        public int? AssetId
        {
            get
            {
                try
                {
                    return int.Parse(GetSimpleSetting(AssetIdKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(AssetIdKey, value.ToString());
        }

        public int? AssetPartitionId
        {
            get
            {
                try
                {
                    return int.Parse(GetSimpleSetting(AssetPartitionIdKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(AssetPartitionIdKey, value.ToString());
        }

        public int? AssetAccountGroupId
        {
            get
            {
                try
                {
                    return int.Parse(GetSimpleSetting(AssetAccountGroupIdKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(AssetAccountGroupIdKey, value.ToString());
        }

        public string LastKnownMonitorState
        {
            get => GetSimpleSetting(LastKnownMonitorStateKey);
            set => SetSimpleSetting(LastKnownMonitorStateKey, value);
        }

        public string LastKnownReverseFlowMonitorState
        {
            get => GetSimpleSetting(LastKnownReverseFlowMonitorStateKey);
            set => SetSimpleSetting(LastKnownReverseFlowMonitorStateKey, value);
        }

        public int ReverseFlowPollingInterval
        {
            get
            {
                try
                {
                    return int.Parse(GetSimpleSetting(ReverseFlowPollingIntervalKey));
                }
                catch
                {
                    return WellKnownData.ReverseFlowMonitorPollingInterval;
                }
            }
            set
            {
                if (value <= 0)
                    value = WellKnownData.ReverseFlowMonitorPollingInterval;
                SetSimpleSetting(ReverseFlowPollingIntervalKey, value.ToString());
            }
        }

        public string SigningCertificate
        {
            get => GetSimpleSetting(SigningCertificateKey);
            set => SetSimpleSetting(SigningCertificateKey, value);
        }

        public string UserCertificateThumbprint
        {
            get => GetSimpleSetting(UserCertificateThumbprintKey);
            set => SetSimpleSetting(UserCertificateThumbprintKey, value);
        }

        public string UserCertificateBase64Data
        {
            get => GetSimpleSetting(UserCertificateDataKey);
            set => SetSimpleSetting(UserCertificateDataKey, value);
        }

        public string UserCertificatePassphrase
        {
            get => GetSimpleSetting(UserCertificatePassphraseKey);
            set => SetSimpleSetting(UserCertificatePassphraseKey, value);
        }

        public string WebSslCertificateBase64Data
        {
            get => GetSimpleSetting(WebSslCertificateDataKey);
            set => SetSimpleSetting(WebSslCertificateDataKey, value);
        }

        public string WebSslCertificatePassphrase
        {
            get => GetSimpleSetting(WebSslCertificatePassphraseKey);
            set => SetSimpleSetting(WebSslCertificatePassphraseKey, value);
        }

        public string UserCsrBase64Data
        {
            get => GetSimpleSetting(UserCsrDataKey);
            set => SetSimpleSetting(UserCsrDataKey, value);
        }

        public string UserCsrPrivateKeyBase64Data
        {
            get => GetSimpleSetting(UserCsrPrivateKeyDataKey);
            set => SetSimpleSetting(UserCsrPrivateKeyDataKey, value);
        }

        public string WebSslCsrBase64Data
        {
            get => GetSimpleSetting(WebSslCsrDataKey);
            set => SetSimpleSetting(WebSslCsrDataKey, value);
        }

        public string WebSslCsrPrivateKeyBase64Data
        {
            get => GetSimpleSetting(WebSslCsrPrivateKeyDataKey);
            set => SetSimpleSetting(WebSslCsrPrivateKeyDataKey, value);
        }

        public X509Certificate2 UserCertificate
        {
            get
            {
                if (!string.IsNullOrEmpty(UserCertificateBase64Data))
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(UserCertificateBase64Data);
                        var cert = CertificateExtensions.LoadFromBytes(bytes,UserCertificatePassphrase);
                        return cert;
                    }
                    catch (Exception)
                    {
                        // TODO: log?
                        // throw appropriate error?
                    }
                }
                else if (!string.IsNullOrEmpty(UserCertificateThumbprint))
                {
                    var store = new X509Store("My", StoreLocation.CurrentUser);
                    try
                    {
                        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                        var certs = store.Certificates
                            .Find(X509FindType.FindByThumbprint, UserCertificateThumbprint, false);
                        if (certs.Count == 1)
                        {
                            return certs[0];
                        }
                    }
                    catch (Exception)
                    {
                        // TODO: log?
                        // throw appropriate error?
                    }
                    finally
                    {
                        store.Close();
                    }
                }

                return null;
            }
        }

        public X509Certificate2 WebSslCertificate
        {
            get
            {
                if (!string.IsNullOrEmpty(WebSslCertificateBase64Data))
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(WebSslCertificateBase64Data);
                        var cert = CertificateExtensions.LoadFromBytes(bytes, WebSslCertificatePassphrase);
                        return cert;
                    }
                    catch (Exception)
                    {
                        // TODO: log?
                        // throw appropriate error?
                    }
                }

                return null;
            }

            set
            {
                if (value != null)
                {
                    WebSslCertificateBase64Data = Convert.ToBase64String(value.Export(X509ContentType.Pfx));
                }
                else
                {
                    WebSslCertificateBase64Data = null;
                }
            }
        }

        public Tuple<string,string> GetWebSslPemCertificate()
        {
            if (!string.IsNullOrEmpty(WebSslCertificateBase64Data))
            {
                try
                {
                    var certPass = string.IsNullOrEmpty(WebSslCertificatePassphrase) ? "" : WebSslCertificatePassphrase;
                    var cert = new CertificateData(WebSslCertificateBase64Data, certPass);
                    return new Tuple<string,string>(cert.PemEncodedCertificate, cert.PemEncodedUnencryptedPrivateKey);
                }
                catch (Exception)
                {
                    // TODO: log?
                    // throw appropriate error?
                }
            }

            return null;
        }

        public DevOpsSecretsBroker DevOpsSecretsBroker
        {
            get
            {
                var devOpsSecretsBroker = new DevOpsSecretsBroker()
                {
                    Host = SafeguardAddress,
                    DevOpsInstanceId = SvcId,
                    A2ARegistration = A2aRegistrationId == null || A2aRegistrationId == 0 
                        ? null 
                        : new A2ARegistration()
                        {
                            DevOpsInstanceId = SvcId,
                            Id = A2aRegistrationId.Value,
                            CertificateUserId = A2aUserId ?? 0,
                            CertificateUserThumbPrint = UserCertificateThumbprint
                        },
                    A2AVaultRegistration = A2aVaultRegistrationId == null || A2aVaultRegistrationId == 0 
                        ? null 
                        : new A2ARegistration()
                        {
                            DevOpsInstanceId = SvcId,
                            Id = A2aVaultRegistrationId.Value,
                            CertificateUserId = A2aUserId ?? 0,
                            CertificateUserThumbPrint = UserCertificateThumbprint
                        },
                    A2AUser = A2aUserId == null || A2aUserId == 0
                        ? null
                        : new A2AUser() 
                            {Id = A2aUserId ?? 0, DisplayName = WellKnownData.DevOpsUserName(SvcId)},
                    AssetPartition = AssetPartitionId == null || AssetPartitionId == 0 
                        ? null 
                        : new AssetPartition()
                        {
                            Id = AssetPartitionId.Value,
                            Name = WellKnownData.DevOpsAssetPartitionName(SvcId)
                        },
                    Asset = AssetId == null || AssetId == 0 
                        ? null 
                        : new Asset()
                        {
                            Id = AssetId.Value,
                            Name = WellKnownData.DevOpsAssetName(SvcId)
                        },
                    Plugins = GetAllPlugins().Select(x => x.ToDevOpsSecretsBrokerPlugin(this))
                };

                var accounts = new List<AssetAccount>();
                var addons = GetAllAddons().ToList();
                foreach (var addon in addons)
                {
                    accounts.AddRange(addon.VaultCredentials.Select(x => new AssetAccount() {Name = x.Key}));
                }
                devOpsSecretsBroker.Accounts = accounts;

                return devOpsSecretsBroker;
            }
        }

        public void CheckPoint()
        {
            _configurationDb.Checkpoint();
        }

        public void DropDatabase()
        {
            Dispose();
            var dbPath = Path.Combine(WellKnownData.ProgramDataPath, WellKnownData.DbFileName);
            File.Delete(dbPath);
            DeletePassword();
            _logger.Information($"Dropped the database at {dbPath}.");

            InitializeDatabase();
        }

        public void Dispose()
        {
            _configurationDb?.Dispose();
            _disposed = true;
            _configurationDb = null;
        }
    }
}
