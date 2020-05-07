using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using Newtonsoft.Json;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using Safeguard = OneIdentity.DevOps.Data.Safeguard;

namespace OneIdentity.DevOps.Logic
{
    class ConnectionContext : IDisposable
    {
        private static ConnectionContext _instance = null;
        private static readonly object InstanceLock = new object();
//        private ConnectionContext _connectionContext = null;


        public static ConnectionContext Instance
        {
            get
            {
                lock (InstanceLock)
                {
                    return _instance ?? (_instance = new ConnectionContext());
                }
            }
        }

        private SecureString _accessToken;
        private string _sessionKey = Guid.NewGuid().ToString();

        public bool IsAuthenticated => _accessToken != null;
        public Safeguard Appliance { get; set; }

        [JsonIgnore]
        public SecureString AccessToken
        {
            get => _accessToken;
            //set => _accessToken = value.Copy();
        }

        public string IdentityProviderName { get; set; }
        public string UserName { get; set; }
        public string[] AdminRoles { get; set; }
        public string A2ARegistrationName { get; set; }

        public string SessionKey
        {
            get => _sessionKey;
        }

        // public ConnectionContext()
        // {
        // }

        // public ConnectionContext(LoggedInUser loggedInUser)
        // {
        //     AdminRoles = loggedInUser.AdminRoles;
        //     UserName = loggedInUser.UserName;
        //     IdentityProviderName = loggedInUser.IdentityProviderName;
        // }

        public void ResetConnectionContext(string accessToken)
        {
            _accessToken = accessToken.ToSecureString().Copy();
        }

        public bool Compare(string token)
        {
            return token.Equals(_accessToken.ToInsecureString());
        }

        public void Dispose()
        {
            AccessToken?.Dispose();
        }


        // public void Add(ManagementConnection managementConnection)
        // {
        //     lock (InstanceLock)
        //     {
        //         var currentConnection = Find(managementConnection);
        //         if (currentConnection != null)
        //             _cache.Remove(currentConnection.SessionKey);
        //         _cache.Add(managementConnection.SessionKey, managementConnection);
        //     }
        // }
        //
        // public ManagementConnection Find(string sessionKey)
        // {
        //     if (sessionKey != null && _cache.ContainsKey(sessionKey))
        //     {
        //         return _cache[sessionKey];
        //     }
        //
        //     return null;
        // }
        //
        // public ManagementConnection FindByToken(string token)
        // {
        //     return _cache.Values.FirstOrDefault(x => x.AccessToken.ToInsecureString().Equals(token));
        // }
        //
        // public ManagementConnection Find(ManagementConnection managementConnection)
        // {
        //     return _cache.Values.FirstOrDefault(x =>
        //         x.Appliance.ApplianceAddress.Equals(managementConnection.Appliance.ApplianceAddress)
        //         && x.IdentityProviderName.Equals(managementConnection.IdentityProviderName) &&
        //         x.UserName.Equals(managementConnection.UserName));
        // }
        //
        // public void Remove(string sessionKey)
        // {
        //     if (sessionKey != null)
        //     {
        //         lock (InstanceLock)
        //         {
        //             _cache.Remove(sessionKey);
        //         }
        //     }
        // }
    }
}
