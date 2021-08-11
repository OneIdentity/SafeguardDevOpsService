using System;
using System.Security;
using Newtonsoft.Json;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Service configuration
    /// </summary>
    public class ServiceConfiguration : IDisposable
    {
        private SecureString _accessToken;
        private string _sessionKey = Guid.NewGuid().ToString();

        /// <summary>
        /// Service is authenticated
        /// </summary>
        public bool IsAuthenticated => _accessToken != null;

        /// <summary>
        /// Safeguard appliance information
        /// </summary>
        public SafeguardDevOpsConnection Appliance { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonIgnore]
        public SecureString AccessToken
        {
            get => _accessToken;
            set => _accessToken = value.Copy();
        }

        /// <summary>
        /// Logged in user
        /// </summary>
        public LoggedInUser User { get; set; }

        /// <summary>
        /// Session key
        /// </summary>
        [JsonIgnore]
        public string SessionKey 
        {
            get => _sessionKey;
        }

        /// <summary>
        /// Safeguard appliance configuration
        /// </summary>
        public ServiceConfiguration()
        {
        }

        /// <summary>
        /// Safeguard appliance configuration
        /// </summary>
        public ServiceConfiguration(LoggedInUser loggedInUser)
        {
            User = loggedInUser;
        }

        /// <summary>
        /// Compare
        /// </summary>
        public bool Compare(string token)
        {
            return token.Equals(_accessToken.ToInsecureString());
        }

        /// <summary>
        /// Clear properties
        /// </summary>
        public void Clear()
        {
            User = null;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            AccessToken?.Dispose();
        }
    }
}
