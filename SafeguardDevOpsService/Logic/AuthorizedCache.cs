using System.Collections.Generic;
using System.Linq;
using OneIdentity.DevOps.Data;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Logic
{
    class AuthorizedCache
    {
        private static AuthorizedCache _instance = null;
        private static readonly object InstanceLock = new object();
        private static readonly Dictionary<string,ServiceConfiguration> _cache = new Dictionary<string,ServiceConfiguration>();

        public static AuthorizedCache Instance
        {
            get
            {
                lock (InstanceLock)
                {
                    return _instance ?? (_instance = new AuthorizedCache());
                }
            }
        }

        public void Add(ServiceConfiguration managementConnection)
        {
            lock (InstanceLock)
            {
                var currentConnection = Find(managementConnection);
                if (currentConnection != null)
                    _cache.Remove(currentConnection.SessionKey);
                _cache.Add(managementConnection.SessionKey, managementConnection);
            }
        }

        public ServiceConfiguration Find(string sessionKey)
        {
            if (sessionKey != null && _cache.ContainsKey(sessionKey))
            {
                return _cache[sessionKey];
            }

            return null;
        }

        public ServiceConfiguration FindByToken(string token)
        {
            return _cache.Values.FirstOrDefault(x => x.AccessToken.ToInsecureString().Equals(token));
        }

        public ServiceConfiguration Find(ServiceConfiguration managementConnection)
        {
            return _cache.Values.FirstOrDefault(x =>
                x.Appliance.ApplianceAddress.Equals(managementConnection.Appliance.ApplianceAddress)
                && x.IdentityProviderName.Equals(managementConnection.IdentityProviderName) &&
                x.UserName.Equals(managementConnection.UserName));
        }

        public void Remove(string sessionKey)
        {
            if (sessionKey != null)
            {
                lock (InstanceLock)
                {
                    _cache.Remove(sessionKey);
                }
            }
        }
    }
}
