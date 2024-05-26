using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using OneIdentity.DevOps.Common;
using Serilog;

namespace GoogleCloudSecretManager
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private Dictionary<string, string> _configuration;

        /// <summary>One of the property keys of the necessary configuration required to access a secret in
        /// Google Cloud Secret Manager.
        /// https://cloud.google.com/resource-manager/docs/creating-managing-projects</summary>
        private const string ProjectId = "ProjectId";

        /// <summary>Prefix log entries with this to help identify their source.</summary>
        private static readonly string LogPrefix = "Google Cloud Secret Manager:";

        public string Name => "GoogleCloudSecretManager";

        public string DisplayName => "Google Cloud Secret Manager";

        public string Description => "Create a service account in Google Cloud and create/download a new service account key " +
            "in JSON format. Add the contents of the JSON file as the password to an account in Safeguard to be used as the " +
            "service account here. In Google Cloud, create or edit a secret and grant the service account the 'Secret Manager " +
            "Secret Accessor' and/or 'Secret Manager Secret Version Adder', depending on the flow.";

        public CredentialType[] SupportedCredentialTypes => new[] { CredentialType.Password };

        public bool SupportsReverseFlow => true;

        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

        private GoogleServiceAccountKey _googleServiceAccount = null;

        private static readonly HttpClient HttpClient = new()
        {
            BaseAddress = new Uri("https://secretmanager.googleapis.com/"),
        };

        /// <summary>Returns a Dictionary that defines the configuration elements that are required by the plugin.
        /// The configuration of every plugin is defined as key/value pairs.</summary>
        /// <returns></returns>
        public Dictionary<string, string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { ProjectId, "" },
            };
        }

        /// <summary>This method is called whenever a new configuration is updated by calling
        /// PUT /service/devops/v1/Plugins/{name} API or when the plugin is initially loaded by the Safeguard
        /// Secrets Broker service.</summary>
        /// <param name="configuration"></param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void SetPluginConfiguration(Dictionary<string, string> configuration)
        {
            _configuration = configuration;
        }

        /// <summary>This method is called before the TestVaultConnection() method is called or the Safeguard
        /// Secrets Broker A2A monitor is enabled. The implementation of this method should establish an
        /// authenticated connection with the third-party vault and store the connection in memory to be used
        /// whenever credentials need to be pushed to the vault.</summary>
        /// <param name="credential">The Google Cloud service account credential, in the form of a JSON object,
        /// with an RSA private key used to create self-signed JWT auth tokens.</param>
        public void SetVaultCredential(string credential)
        {
            // https://developers.google.com/identity/protocols/oauth2/service-account
            // See flow chart at the end of the following page, under the "Decision time" section:
            // https://cloud.google.com/blog/products/identity-security/how-to-authenticate-service-accounts-to-help-keep-applications-secure

            _googleServiceAccount = JsonConvert.DeserializeObject<GoogleServiceAccountKey>(credential);
        }

        /// <summary>This method is called whenever the API /service/devops/v1/Plugins/{name}/TestConnection is
        /// called. The implementation of the method should use the authenticated connection that was established
        /// when the SetVaultCredentials() method was called and test the connectivity to the third-party vault.</summary>
        /// <returns>Returns <c>true</c> if the plugin determines that the specified configuration settings are correct.
        /// Otherwise, <c>false</c>.</returns>
        public bool TestVaultConnection()
        {
            // Unfortunately, without granting additional permissions to the Google Cloud service account, or having
            // information to a specific secret, there isn't an API that we can call to test the connectivity and ensure
            // that the configuration is correct. So we will simply just try using the service account key data to
            // generate a JWT auth token and ensure at least that part is working correctly.
            try
            {
                Logger.Information($"{LogPrefix} Testing configuration for Project Id: {_configuration[ProjectId]}.");

                if (_googleServiceAccount == null)
                {
                    Logger.Error($"{LogPrefix} Could not decode the service account JSON key.");

                    return false;
                }

                if (_googleServiceAccount.Type != GoogleServiceAccountKey.ServiceAccountCredentialType)
                {
                    Logger.Error($"{LogPrefix} The Google Cloud service account key must be of type '{GoogleServiceAccountKey.ServiceAccountCredentialType}'.");

                    return false;
                }

                _ = _googleServiceAccount.GetAuthToken();

                Logger.Information($"{LogPrefix} Successfully created auth token for Project Id: {_configuration[ProjectId]}. However, no test connection can be done at this time. Proceed with adding and managing a secret.");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"{LogPrefix} Error while testing the configuration.");
            }

            return false;

            // Possible actions that can be performed to test the connection and configuration.

            // https://cloud.google.com/secret-manager/docs/reference/rest/v1/projects.secrets
            // 1. get: https://cloud.google.com/secret-manager/docs/reference/rest/v1/projects.secrets/get
            //    This seems the most logical, but obviously requires that both the project Id and secret name
            //    be specified in the configuration.
            // 
            // 2. list: https://cloud.google.com/secret-manager/docs/reference/rest/v1/projects.secrets/list
            //    This could test the authentication, but not necessarily the specifically named secret.
            //    Perhaps when getting back the list, we could check that the secret name exists. But that
            //    still wouldn't necessarily prove that we have access to it.
            //    Authorization requires the following IAM permission on the specified resource parent:
            //       secretmanager.secrets.list
            //
            //    GET https://secretmanager.googleapis.com/v1/projects/devtest-oid-rnd-amer-safeguard/secrets
            //
            // 3. testIamPermissions: https://cloud.google.com/secret-manager/docs/reference/rest/v1/projects.secrets/testIamPermissions
            //    This could be good as well and serve as a means of debugging as well. We would have to specify
            //    in the request what permission(s) we want to check for, which seems reasonable. But the documentation
            //    also mentions needing the following OAuth scope: https://www.googleapis.com/auth/cloud-platform
            //    Not sure if we'll have that.
            //    Yes, we have that OAuth scope.
            //    POST https://secretmanager.googleapis.com/v1/projects/devtest-oid-rnd-amer-safeguard/secrets/:testIamPermissions
            //
            // https://developers.google.com/identity/protocols/oauth2/service-account#jwt-auth

            // When using a least privileged service account, it is possible, and recommended to only give the
            // service account access to the APIs, objects, and methods/operations on those objects that are
            // necessary, and nothing more. For example, if we want the service account to only be able to retrieve
            // a version of a single secret, we can set the permissions to do so. At which point it won't even be
            // able to get the metadata on the secret, since those are two different operations, with their own
            // permissions.
            //
            // There might be a way to call one of the Google OAuth end points to exchange the JWT, but that may
            // require its own permissions grant. We could document and suggest that as a requirement. However,
            // another possibility could be to rely upon getting a specific error. After some simple testing, it
            // seems that if we make a request with a valid, non-expired JWT, to a secret that doesn't exist, we
            // will get back a 403 Forbidden error, as opposed to a 401 Unauthorized error when we use an expired
            // or completely invalid JWT.
            //
            // Darn, it doesn't matter what project ID is used either, we still get back a 403. So this probably
            // isn't a very good approach.

            // https://secretmanager.googleapis.com/v1/projects/devtest-oid-rnd-amer-safeguard/secrets/SafeguardSecretsBroker2/versions/latest
            //var path = $"v1/projects/{_configuration[ProjectId]}/secrets/SafeguardSecretsBrokerTestVaultConnection/versions/latest:access";

            //var request = new HttpRequestMessage
            //{
            //    Method = HttpMethod.Get,
            //    RequestUri = new Uri(path, UriKind.Relative),
            //};

            //request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            //var response = HttpClient.Send(request);

            //return response.StatusCode == System.Net.HttpStatusCode.Forbidden;
        }

        /// <summary>This method is called immediately after the monitor has been enabled, when the Safeguard Secrets
        /// Broker has been notified that a monitored credential changed and when a new credential needs to be pushed
        /// to the corresponding vault. The implementation of this method should use the established connection to the
        /// vault to push the new credential as the specified account name. </summary>
        /// <param name="credentialType"></param>
        /// <param name="asset"></param>
        /// <param name="account"></param>
        /// <param name="credential"></param>
        /// <param name="altAccountName"></param>
        /// <returns></returns>
        public string SetCredential(CredentialType credentialType, string asset, string account, string[] credential, string altAccountName)
        {
            Logger.Information($"{LogPrefix} Setting credential for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}");

            // https://cloud.google.com/secret-manager/docs/reference/rest/v1/projects.secrets/addVersion
            // POST https://secretmanager.googleapis.com/v1/projects/{project-id}/secrets/{my-secret}:addVersion
            //
            // {
            //   "payload": {
            //     "data": "A base-64 encoded string",
            //     "dataCrc32c": optional
            //   }
            // }
            var payload = new
            {
                payload = new
                {
                    data = Convert.ToBase64String(Encoding.UTF8.GetBytes(credential[0]))
                }
            };

            // The delegated service account requires the "Secret Manager Secret Version Adder" permission in order to
            // call this Google Cloud Secret Manager API method and be able to effectively change the value of the secret.
            // No other permission should be needed, unless using the same delegated service account for other actions.
            var path = $"v1/projects/{_configuration[ProjectId]}/secrets/{altAccountName ?? account}:addVersion";

            using var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(path, UriKind.Relative),
                Content = jsonContent,
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _googleServiceAccount.GetAuthToken());
            
            var response = HttpClient.Send(request);

            return response.StatusCode == System.Net.HttpStatusCode.OK ? credential[0] : null;
        }

        /// <summary>This method is called immediately after the monitor has been enabled and on a polling
        /// schedule that defaults every 60 seconds. This method is not called if the plugin does not support
        /// the Reverse Flow functionality or the plugin instance does not have the Reverse Flow functionality
        /// enabled.</summary>
        /// <param name="credentialType"></param>
        /// <param name="asset"></param>
        /// <param name="account"></param>
        /// <param name="altAccountName"></param>
        /// <returns></returns>
        public string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName)
        {
            // For reverse flow only.
            Logger.Information($"{LogPrefix} Getting credential for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}.");

            // The delegated service account requires the "Secret Manager Secret Accessor" permission in order to
            // call this Google Cloud Secret Manager API method and be able to get the latest version of the secret.
            // No other permission should be needed, unless using the same delegated service account for other actions.
            var path = $"v1/projects/{_configuration[ProjectId]}/secrets/{altAccountName ?? account}/versions/latest:access";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(path, UriKind.Relative),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _googleServiceAccount.GetAuthToken());

            var response = HttpClient.Send(request);

            if (!response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                Logger.Error($"{LogPrefix} Error getting credential for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}.\r\n{content}");

                return null;
            }

            var secret = response.Content.ReadFromJsonAsync<AccessSecretVersion>().GetAwaiter().GetResult();

            if (secret == null)
            {
                Logger.Error($"{LogPrefix} Unknown response when getting credential for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}.");

                return null;
            }

            return secret.PayloadAsString();
        }

        /// <summary>This method is called whenever the Safeguard Secrets Broker service is restarted or shutdown.
        /// The implementation of this method should include anything that needs to be done to the plugin to cleanly
        /// shutdown.</summary>
        public void Unload()
        {
            // No resources to clean up or do here.
        }
    }

    /// <summary>The response object when accessing a version of a secret.</summary>
    /// <remarks>https://cloud.google.com/secret-manager/docs/reference/rest/v1/projects.secrets.versions/access</remarks>
    public class AccessSecretVersion
    {
        /// <summary>The resource name of the SecretVersion in the format projects/*/secrets/*/versions/*.</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>Secret payload.</summary>
        [JsonProperty("payload")]
        public SecretPayload Payload { get; set; }

        /// <summary>Returns the secret data as a UTF-8 encoded string.</summary>
        /// <returns></returns>
        public string PayloadAsString()
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(Payload.Data));
        }
    }

    /// <summary>A secret payload resource in the Secret Manager API. This contains the sensitive secret payload that
    /// is associated with a SecretVersion.</summary>
    /// <remarks>https://cloud.google.com/secret-manager/docs/reference/rest/v1/SecretPayload</remarks>
    public class SecretPayload
    {
        /// <summary>The secret data. Must be no larger than 64KiB. A base64-encoded string.</summary>
        [JsonProperty("data")]
        public string Data { get; set; }
    }

    /// <summary>In the Google Cloud console portal, https://console.cloud.google.com, navigate to IAM & Admin → Service Accounts.
    /// Or just search for Service Accounts. Then choose to create a new service account. After the service account is created,
    /// go to the Keys tab and click the Add Key to create a new key pair. Choose JSON for the key type. The private key will be
    /// downloaded and saved to your local computer as a file. The contents of that file need to be set as the password of an
    /// account in Safeguard that will be used as the service account for managing the secrets in Google.
    /// <para>This class simply deserializes that JSON and provides methods for generating a JWT auth token to be used when calling
    /// the Google Secret Manager API.</para></summary>
    public class GoogleServiceAccountKey
    {
        public static readonly string ServiceAccountCredentialType = "service_account";

        /// <summary>Type of the credential. The value must be <see cref="ServiceAccountCredentialType"/>.</summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Project ID associated with this credential.
        /// </summary>
        [JsonProperty("project_id")]
        public string ProjectId { get; set; }

        /// <summary>
        /// Universe domain that this credential may be used in.
        /// </summary>
        [JsonProperty("universe_domain")]
        public string UniverseDomain { get; set; }

        /// <summary>
        /// Client Id associated with UserCredential created by
        /// <a href="https://cloud.google.com/sdk/gcloud/reference/auth/login">GCloud Auth Login</a>
        /// or with an external account credential.
        /// </summary>
        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        /// <summary>
        /// Client Email associated with ServiceAccountCredential obtained from
        /// <a href="https://console.developers.google.com">Google Developers Console</a>
        /// </summary>
        [JsonProperty("client_email")]
        public string ClientEmail { get; set; }

        /// <summary>
        /// Private Key associated with ServiceAccountCredential obtained from
        /// <a href="https://console.developers.google.com">Google Developers Console</a>.
        /// </summary>
        [JsonProperty("private_key")]
        public string PrivateKey { get; set; }

        /// <summary>
        /// Private Key ID associated with ServiceAccountCredential obtained from
        /// <a href="https://console.developers.google.com">Google Developers Console</a>.
        /// </summary>
        [JsonProperty("private_key_id")]
        public string PrivateKeyId { get; set; }

        /// <summary>
        /// The token endpoint for a service account credential.
        /// </summary>
        /// <remarks>
        /// Note that this is different from TokenUrl which is the
        /// STS token exchange endpoint associated with an external account credential.
        /// </remarks>
        [JsonProperty("token_uri")]
        public string TokenUri { get; set; }

        /// <summary>Google requires, and only allows a 1 hour lifetime/expiration on the JWT token.</summary>
        private static readonly int TokenLifetimeSeconds = 3600;

        private RSA _signingKey = null;

        private string _jwtToken = null;

        private long _lastIssued = 0;

        /// <summary>Ensure only a single threads creates a token at a time.</summary>
        private readonly object JwtLock = new object();

        public string GetAuthToken()
        {
            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (HaveValidJwt(nowSeconds))
            {
                return _jwtToken;
            }

            lock (JwtLock)
            {
                if (HaveValidJwt(nowSeconds))
                {
                    return _jwtToken;
                }

                _lastIssued = nowSeconds;
                _jwtToken = BuildJwt(nowSeconds);

                return _jwtToken;
            }
        }

        private bool HaveValidJwt(long now)
        {
            var isExpired = (_lastIssued + TokenLifetimeSeconds) < now;

            return _jwtToken != null && !isExpired;
        }

        private string BuildJwt(long issuedAt)
        {
            var header = new
            {
                alg = "RS256",
                kid = PrivateKeyId,
                typ = "JWT",
            };

            var payload = new
            {
                scope = "https://www.googleapis.com/auth/cloud-platform",
                email_verified = false,
                iss = ClientEmail,
                sub = ClientEmail,
                iat = issuedAt,
                exp = issuedAt + TokenLifetimeSeconds,
                aud = "https://secretmanager.googleapis.com/",
            };

            var serializedHeader = JwtEncode(JsonConvert.SerializeObject(header));
            var serializedPayload = JwtEncode(JsonConvert.SerializeObject(payload));

            var jwtData = String.Concat(serializedHeader, ".", serializedPayload);

            var jwtSignature = SignJwt(jwtData);

            return String.Concat(jwtData, ".", jwtSignature);
        }

        private string SignJwt(string data)
        {
            // This should only ever have to be done once, for the lifetime of the object. The actual
            // private key never changes. Only the generated auth tokens, that have a lifetime of 1 hour,
            // need to be periodically regenerated.
            if (_signingKey == null)
            {
                _signingKey = RSA.Create();

                _signingKey.ImportFromPem(PrivateKey);
            }

            using (var hashAlg = SHA256.Create())
            {
                var hash = hashAlg.ComputeHash(Encoding.ASCII.GetBytes(data));
                var sigBytes = _signingKey.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                return JwtEncode(sigBytes);
            }
        }

        private static string JwtEncode(string value)
        {
            return JwtEncode(Encoding.UTF8.GetBytes(value));
        }

        private static string JwtEncode(byte[] value)
        {
            return Convert.ToBase64String(value)
                .Replace("=", String.Empty).Replace('+', '-').Replace('/', '_');
        }
    }
}
