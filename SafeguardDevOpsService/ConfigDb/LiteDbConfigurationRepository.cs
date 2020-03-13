using System;
using System.Collections.Generic;
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

        public void Dispose()
        {
            _configurationDb?.Dispose();
            _disposed = true;
            _configurationDb = null;
        }
    }
}
