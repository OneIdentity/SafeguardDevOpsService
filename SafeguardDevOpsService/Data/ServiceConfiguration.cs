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
        public SafeguardConnection Appliance { get; set; }

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
        /// Identity provider name
        /// </summary>
        public string IdentityProviderName { get; set; }
        /// <summary>
        /// User name
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// Admin roles
        /// </summary>
        public string[] AdminRoles { get; set; }
        /// <summary>
        /// A2A registration name
        /// </summary>
        public string A2ARegistrationName { get; set; }
        /// <summary>
        /// Thumb print
        /// </summary>
        public string Thumbprint { get; set; }

        /// <summary>
        /// Session key
        /// </summary>
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
            AdminRoles = loggedInUser.AdminRoles;
            UserName = loggedInUser.UserName;
            IdentityProviderName = loggedInUser.IdentityProviderName;
        }

        /// <summary>
        /// Compare
        /// </summary>
        public bool Compare(string token)
        {
            return token.Equals(_accessToken.ToInsecureString());
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
