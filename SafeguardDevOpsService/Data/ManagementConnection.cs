using System;
using System.Security;
using Newtonsoft.Json;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Data
{
    public class ManagementConnection : IDisposable
    {
        private SecureString _accessToken;
        private string _sessionKey = Guid.NewGuid().ToString();

        public bool IsAuthenticated => _accessToken != null;
        public Safeguard Appliance { get; set; }

        [JsonIgnore]
        public SecureString AccessToken
        {
            get => _accessToken;
            set => _accessToken = value.Copy();
        }
        public string IdentityProviderName { get; set; }
        public string UserName { get; set; }
        public string[] AdminRoles { get; set; }

        public string SessionKey
        {
            get => _sessionKey;
        }

        public ManagementConnection()
        {
        }

        public ManagementConnection(LoggedInUser loggedInUser)
        {
            AdminRoles = loggedInUser.AdminRoles;
            UserName = loggedInUser.UserName;
            IdentityProviderName = loggedInUser.IdentityProviderName;
        }

        public bool Compare(string token)
        {
            return token.Equals(_accessToken.ToInsecureString());
        }

        public void Dispose()
        {
            AccessToken?.Dispose();
        }
    }
}
