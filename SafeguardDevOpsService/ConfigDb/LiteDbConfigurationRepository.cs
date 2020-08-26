using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CredentialManagement;
using LiteDB;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.ConfigDb
{
    internal class LiteDbConfigurationRepository : IConfigurationRepository, IDisposable
    {
        private bool _disposed;
        private LiteDatabase _configurationDb;
        private X509Certificate2Collection _trustedCertificateCollection = null;
        private ILiteCollection<Setting> _settings;
        private ILiteCollection<AccountMapping> _accountMappings;
        private ILiteCollection<Plugin> _plugins;
        private ILiteCollection<TrustedCertificate> _trustedCertificates;
        private string _svcId;

        private const string DbFileName = "Configuration.db";

        private const string SettingsTableName = "settings";
        private const string AccountMappingsTableName = "accountmappings";
        private const string PluginsTableName = "plugins";
        private const string TrustedCertificateTableName = "trustedcertificates";

        private const string SafeguardAddressKey = "SafeguardAddress";
        private const string ApiVersionKey = "ApiVersion";
        private const string IgnoreSslKey = "IgnoreSsl";
        private const string A2aUserIdKey = "A2aUserId";
        private const string A2aRegistrationIdKey = "A2aRegistrationId";
        private const string A2aVaultRegistrationIdKey = "A2aVaultRegistrationId";
        private const string SigningCertificateKey = "SigningCertificate";

        private const string UserCertificateThumbprintKey = "UserCertThumbprint";
        private const string UserCertificateDataKey = "UserCertData";
        private const string UserCertificatePassphraseKey = "UserCertPassphrase";
        private const string UserCsrDataKey = "UserCertificateSigningRequestData";
        private const string UserCsrPrivateKeyDataKey = "UserCertificateSigningRequestPrivateKeyData";
        private const string WebSslCertificateDataKey = "WebSslCertData";
        private const string WebSslCertificatePassphraseKey = "WebSslCertPassphrase";
        private const string WebSslCsrDataKey = "WebSslCertificateSigningRequestData";
        private const string WebSslCsrPrivateKeyDataKey = "WebSslCertificateSigningRequestPrivateKeyData";

        public LiteDbConfigurationRepository()
        {
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!Directory.Exists(WellKnownData.ProgramDataPath))
                Directory.CreateDirectory(WellKnownData.ProgramDataPath);
            var dbPath = Path.Combine(WellKnownData.ProgramDataPath, DbFileName);
            Serilog.Log.Logger.Information($"Loading configuration database at {dbPath}.");

            var passwd = GetPassword();
            if (string.IsNullOrEmpty(passwd))
            {
                passwd = SavePassword(GeneratePassword());
            }

            var connectionString = $"Filename={dbPath};Password={passwd}";
            _configurationDb = new LiteDatabase(connectionString);
            _disposed = false;
            _settings = _configurationDb.GetCollection<Setting>(SettingsTableName);
            _accountMappings = _configurationDb.GetCollection<AccountMapping>(AccountMappingsTableName);
            _plugins = _configurationDb.GetCollection<Plugin>(PluginsTableName);
            _trustedCertificates = _configurationDb.GetCollection<TrustedCertificate>(TrustedCertificateTableName);
        }

        private string GeneratePassword()
        {
            var random = new byte[24];
            var rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(random);
            return Convert.ToBase64String(random);
        }

        private string SavePassword(string password)
        {
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
                Serilog.Log.Logger.Error(msg);
                throw new DevOpsException(msg);
            }
        }

        private string GetPassword()
        {
            using (var cred = new Credential())
            {
                cred.Target = WellKnownData.CredentialTarget;
                cred.Load();
                return cred.Password;
            }
        }

        private void DeletePassword()
        {
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

        public Plugin SavePluginConfiguration(Plugin plugin)
        {
            _plugins.Upsert(plugin);
            return plugin;
        }

        public void DeletePluginByName(string name)
        {
            _plugins.Delete(name);
        }

        public IEnumerable<AccountMapping> GetAccountMappings()
        {
            return _accountMappings.FindAll();
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
                    VerificationFlags = X509VerificationFlags.IgnoreRootRevocationUnknown | X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown,
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
                        Serilog.Log.Logger.Error(msg, ex);
                        throw new DevOpsException(msg, ex);
                    }
                }

                return _svcId;
            }
        }

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
                    return Int32.Parse(GetSimpleSetting(A2aUserIdKey));
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
                    return Int32.Parse(GetSimpleSetting(A2aRegistrationIdKey));
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
                    return Int32.Parse(GetSimpleSetting(A2aVaultRegistrationIdKey));
                }
                catch
                {
                    return null;
                }
            }
            set => SetSimpleSetting(A2aVaultRegistrationIdKey, value.ToString());
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
                        var cert = string.IsNullOrEmpty(UserCertificatePassphrase)
                            ? new X509Certificate2(bytes)
                            : new X509Certificate2(bytes, UserCertificatePassphrase);
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
                        var cert = string.IsNullOrEmpty(WebSslCertificatePassphrase)
                            ? new X509Certificate2(bytes)
                            : new X509Certificate2(bytes, WebSslCertificatePassphrase);
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

        public void DropDatabase()
        {
            Dispose();
            var dbPath = Path.Combine(WellKnownData.ProgramDataPath, DbFileName);
            File.Delete(dbPath);
            DeletePassword();
            Serilog.Log.Logger.Information($"Dropped the database at {dbPath}.");

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
