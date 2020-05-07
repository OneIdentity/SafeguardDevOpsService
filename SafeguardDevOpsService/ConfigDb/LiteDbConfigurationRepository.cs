using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using LiteDB;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.ConfigDb
{
    internal class LiteDbConfigurationRepository : IConfigurationRepository, IDisposable
    {
        private bool _disposed;
        private LiteDatabase _configurationDb;
        private readonly ILiteCollection<Setting> _settings;
        private readonly ILiteCollection<Registration> _registrations;
        private readonly ILiteCollection<AccountMapping> _accountMappings;
        private readonly ILiteCollection<Plugin> _plugins;

        private const string DbFileName = "Configuration.db";

        private const string SettingsTableName = "settings";
        private const string RegistrationsTableName = "registrations";
        private const string AccountMappingsTableName = "accountmappings";
        private const string PluginsTableName = "plugins";

        private const string SafeguardAddressKey = "SafeguardAddress";
        private const string ApiVersionKey = "ApiVersion";
        private const string IgnoreSslKey = "IgnoreSsl";
        private const string A2aUserIdKey = "A2aUserId";
        private const string A2aRegistrationIdKey = "A2aRegistrationId";
        private const string SigningCertifcateKey = "SigningCertificate";

        private const string UserCertificateThumbprintKey = "UserCertThumbprint";
        private const string UserCertificateDataKey = "UserCertData";
        private const string UserCertificatePassphraseKey = "UserCertPassphrase";
        private const string CsrDataKey = "CertificateSigningRequestData";
        private const string CsrPrivateKeyDataKey = "CertificateSigningRequestPrivateKeyData";

        public LiteDbConfigurationRepository()
        {
            _configurationDb = new LiteDatabase(DbFileName);
            _settings = _configurationDb.GetCollection<Setting>(SettingsTableName);
            _registrations = _configurationDb.GetCollection<Registration>(RegistrationsTableName);
            _accountMappings = _configurationDb.GetCollection<AccountMapping>(AccountMappingsTableName);
            _plugins = _configurationDb.GetCollection<Plugin>(PluginsTableName);
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

        // TODO: fix
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

        public void getAccountMappingsByName(string name)
        {
            _accountMappings.FindById(name);
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

        public string SigningCertificate
        {
            get => GetSimpleSetting(SigningCertifcateKey);
            set => SetSimpleSetting(SigningCertifcateKey, value);
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

        public string CsrBase64Data
        {
            get => GetSimpleSetting(CsrDataKey);
            set => SetSimpleSetting(CsrDataKey, value);
        }

        public string CsrPrivateKeyBase64Data
        {
            get => GetSimpleSetting(CsrPrivateKeyDataKey);
            set => SetSimpleSetting(CsrPrivateKeyDataKey, value);
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

            set
            {
                if (value != null)
                {
                    UserCertificateBase64Data = string.IsNullOrEmpty(UserCertificatePassphrase) 
                        ? Convert.ToBase64String(value.Export(X509ContentType.Pfx)) 
                        : Convert.ToBase64String(value.Export(X509ContentType.Pfx, UserCertificatePassphrase));
                }
                else
                {
                    UserCertificateBase64Data = null;
                    UserCertificateThumbprint = null;
                }
            }
        }

        public void Dispose()
        {
            _configurationDb?.Dispose();
            _disposed = true;
            _configurationDb = null;
        }
    }
}
