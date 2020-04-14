using System;
using System.Security;
using Newtonsoft.Json;

namespace OneIdentity.DevOps.Data
{
    public class ManagementConnection : IDisposable
    {
        private SecureString _accessToken;

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

        public void Dispose()
        {
            AccessToken?.Dispose();
        }
    }
}
