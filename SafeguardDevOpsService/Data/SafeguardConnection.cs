using System;
using System.Security;
using Newtonsoft.Json;

namespace OneIdentity.DevOps.Data
{
    public class SafeguardConnection : IDisposable
    {
        private SecureString _accessToken;

        public bool IsAuthenticated => _accessToken != null;
        public string ApplianceId { get; set; }
        public string ApplianceName { get; set; }
        public string ApplianceVersion { get; set; }

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
