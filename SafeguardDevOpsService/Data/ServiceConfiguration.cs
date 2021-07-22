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
        private A2ARegistration _a2ARegistration;
        private A2ARegistration _a2AVaultRegistration;
        private A2AUser _a2AUser;
        private Asset _asset;
        private AssetPartition _assetPartition;

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
        /// Identity provider name
        /// </summary>
        public string IdentityProviderName => _a2AUser?.IdentityProviderName;

        /// <summary>
        /// User name
        /// </summary>
        public string UserName => _a2AUser?.UserName;

        /// <summary>
        /// User display name
        /// </summary>
        public string UserDisplayName => _a2AUser?.DisplayName;

        /// <summary>
        /// Thumb print
        /// </summary>
        public string Thumbprint => _a2AUser?.PrimaryAuthenticationIdentity;

        /// <summary>
        /// Admin roles
        /// </summary>
        public string[] AdminRoles => _a2AUser?.AdminRoles;

        /// <summary>
        /// A2A Certificate User
        /// </summary>
        public A2AUser A2AUser
        {
            get => _a2AUser; 
            set => _a2AUser = value;
        }

        /// <summary>
        /// A2A registration name
        /// </summary>
        public string A2ARegistrationName => _a2ARegistration?.AppName;

        /// <summary>
        /// A2A vault registration name
        /// </summary>
        public string A2AVaultRegistrationName => _a2AVaultRegistration?.AppName;

        /// <summary>
        /// A2A registration
        /// </summary>
        public A2ARegistration A2ARegistration
        {
            get => _a2ARegistration; 
            set => _a2ARegistration = value;
        }

        /// <summary>
        /// A2A vault registration
        /// </summary>
        public A2ARegistration A2AVaultRegistration
        {
            get => _a2AVaultRegistration; 
            set => _a2AVaultRegistration = value;
        }

        /// <summary>
        /// Asset
        /// </summary>
        public Asset Asset
        {
            get => _asset; 
            set => _asset = value;
        }

        /// <summary>
        /// Asset partition
        /// </summary>
        public AssetPartition AssetPartition
        {
            get => _assetPartition; 
            set => _assetPartition = value;
        }

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
            _a2AUser = new A2AUser()
            {
                AdminRoles = loggedInUser.AdminRoles,
                UserName = loggedInUser.UserName,
                IdentityProviderName = loggedInUser.IdentityProviderName,
                Id = loggedInUser.Id
            };
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
            _a2AUser = null;
            _a2ARegistration = null;
            _a2AVaultRegistration = null;
            _asset = null;
            _assetPartition = null;
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
