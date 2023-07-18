using System.Collections.Generic;
using System.Linq;
using OneIdentity.DevOps.Data;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Logic
{
    internal class AuthorizedCache
    {
        private static AuthorizedCache _instance;
        private static readonly object InstanceLock = new object();
        private static readonly Dictionary<string,ServiceConfiguration> Cache = new Dictionary<string,ServiceConfiguration>();

        public static AuthorizedCache Instance
        {
            get
            {
                lock (InstanceLock)
                {
                    return _instance ??= new AuthorizedCache();
                }
            }
        }

        public void Add(ServiceConfiguration managementConnection)
        {
            lock (InstanceLock)
            {
                var currentConnection = Find(managementConnection) ?? Find(managementConnection.SessionKey);
                if (currentConnection != null)
                    Cache.Remove(currentConnection.SessionKey);
                Cache.Add(managementConnection.SessionKey, managementConnection);
            }
        }

        public ServiceConfiguration Find(string sessionKey)
        {
            if (sessionKey != null && Cache.ContainsKey(sessionKey))
            {
                return Cache[sessionKey];
            }

            return null;
        }

        public ServiceConfiguration FindByToken(string token)
        {
            return Cache.Values.FirstOrDefault(x => x.AccessToken.ToInsecureString().Equals(token));
        }

        public ServiceConfiguration Find(ServiceConfiguration managementConnection)
        {
            try
            {
                return Cache.Values.FirstOrDefault(x =>
                    x.Appliance.ApplianceAddress.Equals(managementConnection.Appliance.ApplianceAddress)
                    && x.User?.PrimaryAuthenticationProvider?.Name != null
                    && x.User.PrimaryAuthenticationProvider.Name.Equals(managementConnection.User
                        ?.PrimaryAuthenticationProvider?.Name)
                    && x.User?.Name != null
                    && x.User.Name.Equals(managementConnection.User?.Name));
            } catch { }

            return null;
        }

        public void Remove(string sessionKey)
        {
            if (sessionKey != null)
            {
                lock (InstanceLock)
                {
                    Cache.Remove(sessionKey);
                }
            }
        }

        public void Clear()
        {
            lock (InstanceLock)
            {
                Cache.Clear();
            }
        }

    }
}
