using OneIdentity.DevOps.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    internal class CredentialManager : ICredentialManager
    {
        private static readonly Dictionary<string,CachedCredential> CachedCredentials = new Dictionary<string, CachedCredential>();

        private readonly Serilog.ILogger _logger;
        private readonly string _salt;
        private string _uniqueKey(string key, CredentialType type) => key+type;

        public CredentialManager()
        {
            _logger = Serilog.Log.Logger;
            _salt = DateTimeOffset.Now.Millisecond.ToString();
        }

        public void Upsert(string credential, AccountMapping account, CredentialType credentialType)
        {
            var uniqueKey = _uniqueKey(account.Key, credentialType);
            if (CachedCredentials.ContainsKey(uniqueKey))
            {
                var hashValue = HashText(credential);
                if (!CachedCredentials[uniqueKey].Credential.Equals(hashValue))
                {
                    CachedCredentials[uniqueKey].Credential = HashText(credential);
                }
            }
            else 
            {
                var credentialEntry = new CachedCredential()
                {
                    Account = CloneAccount(account),
                    CredentialType = credentialType,
                    Credential = HashText(credential)
                };

                CachedCredentials.Add(uniqueKey, credentialEntry);
            }
        }

        public bool Matches(string credential, AccountMapping account, CredentialType credentialType)
        {
            var uniqueKey = _uniqueKey(account.Key, credentialType);
            if (CachedCredentials.ContainsKey(uniqueKey))
            {
                return CachedCredentials[uniqueKey].Credential.Equals(HashText(credential));
            }
            
            return false;
        }

        public void Clear()
        {
            CachedCredentials.Clear();
        }

        private string HashText(string credential)
        {
            var textWithSaltBytes = Encoding.UTF8.GetBytes(string.Concat(credential, _salt));
            var hashedBytes = SHA256.HashData(textWithSaltBytes);
            return Convert.ToBase64String(hashedBytes);
        }

        private AccountMapping CloneAccount(AccountMapping account)
        {
            var s = JsonConvert.SerializeObject(account);
            return JsonConvert.DeserializeObject<AccountMapping>(s);
        }
    }
}
