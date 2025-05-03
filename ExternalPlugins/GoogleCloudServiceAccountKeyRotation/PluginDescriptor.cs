using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using OneIdentity.DevOps.Common;
using Serilog;

namespace GoogleCloudServiceAccountKeyRotation
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private Dictionary<string, string> _configuration;

        /// <summary>One of the property keys of the necessary configuration required to access
        /// Google Cloud.
        /// https://cloud.google.com/resource-manager/docs/creating-managing-projects</summary>
        private const string ProjectId = nameof(ProjectId);

        /// <summary>The interval on which Google Cloud Service Account keys will be rotated.</summary>
        /// <remarks>Secrets Broker will actually invoke the Reverse Flow every minute, which is what
        /// performs the key rotation in this plug-in. But we want to lengthen and provided it as a
        /// configuration option.
        /// When starting Secrets Broker, however, or when enabling the monitoring, key rotation will
        /// happen immediately.</remarks>
        private const string KeyRotationInDays = nameof(KeyRotationInDays);

        /// <summary>Prefix log entries with this to help identify their source.</summary>
        private static readonly string LogPrefix = "Google Cloud Service Account Key Rotation:";

        public string Name => "GoogleCloudServiceAccountKeyRotation";

        public string DisplayName => "Google Cloud Service Account Key Rotation";

        public string Description => "This is the Google Cloud Service Account Key plug-in for rotating " +
            "Google Cloud Service Account keys.";

        public CredentialType[] SupportedCredentialTypes => new[] { CredentialType.Password };

        public bool SupportsReverseFlow => true;

        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = true;
        public ILogger Logger { get; set; }

        private bool _debugLog = false;

        private GoogleServiceAccountKey _googleServiceAccount = null;

        private static readonly HttpClient HttpClient = new()
        {
            BaseAddress = new Uri("https://iam.googleapis.com/"),
        };

        private int _keyRotationDays = 1;

        /// <summary>Each account that is changed will be added here along with the time. Each
        /// interval that Secrets Broker invokes the method, we'll lookup the account in this
        /// cache and compare the time to <see cref="KeyRotationInDays"/>. If an item doesn't
        /// exist in the cache, we'll change its key and then add it to the cache.</summary>
        /// <remarks>This is never cleaned up. But that would only become a problem if accounts
        /// were configured and then un-configured and the service was never restarted.</remarks>
        private ConcurrentDictionary<string, DateTime> _lastChangedCache = new ConcurrentDictionary<string, DateTime>();

        private static readonly string DevOpsServiceName = "SafeguardDevOpsService";
        private static readonly string ProgramDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        private static readonly string PluginDirName = "ExternalPlugins";

        private readonly string _lastChangedCacheFilename = null;

        // ==================================================
        //
        // If your Google Cloud was created after May 3, 2024, you will need to remove the
        // iam.disableServiceAccountKeyCreation constraint.
        // https://cloud.google.com/resource-manager/docs/organization-policy/restricting-service-accounts#disable_service_account_key_creation
        //
        // If iam.disableServiceAccountKeyCreation is enforced, creating a service account will fail with the error:
        // FAILED_PRECONDITION: Key creation is not allowed on this service account.

        // Also consider an expiry time for all newly created keys in your project.
        // https://cloud.google.com/resource-manager/docs/organization-policy/restricting-service-accounts#limit_key_expiry

        // Enable the IAM API
        // https://cloud.google.com/iam/docs/keys-disable-enable#before-you-begin
        // or
        // https://console.cloud.google.com/apis/dashboard?project=<your project>
        // then click the Enable APIs and Services link at the top.

        // REST API:
        // https://cloud.google.com/iam/docs/keys-create-delete#rest

        // Required Roles:
        // roles/iam.serviceAccountKeyAdmin

        // ==================================================

        public PluginDescriptor()
        {
            _lastChangedCacheFilename = Path.Combine(ProgramDataPath, DevOpsServiceName, PluginDirName, Name, "lastChangedCache.json");
        }

        /// <summary>Returns a Dictionary that defines the configuration elements that are required by the plug-in.
        /// The configuration of every plug-in is defined as key/value pairs.</summary>
        /// <returns></returns>
        public Dictionary<string, string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { ProjectId, "" },
                { KeyRotationInDays, "1" },
            };
        }

        /// <summary>This method is called whenever a new configuration is updated by calling
        /// PUT /service/devops/v1/Plugins/{name} API or when the plugin is initially loaded by the Safeguard
        /// Secrets Broker service.</summary>
        /// <param name="configuration"></param>
        public void SetPluginConfiguration(Dictionary<string, string> configuration)
        {
            DebugLog($"{nameof(SetPluginConfiguration)} being called.");

            if (!configuration.ContainsKey(ProjectId))
            {
                var msg = $"{LogPrefix} Configuration must contain a value for {ProjectId}.";

                Logger.Error(msg);

                throw new ArgumentException(msg);
            }

            if (!configuration.ContainsKey(KeyRotationInDays))
            {
                var msg = $"{LogPrefix} Configuration must contain a value for {KeyRotationInDays}.";

                Logger.Error(msg);

                throw new ArgumentException(msg);
            }

            if (!Int32.TryParse(configuration[KeyRotationInDays], out _keyRotationDays))
            {
                var msg = $"{LogPrefix} Could not parse {KeyRotationInDays} as a number.";

                Logger.Error(msg);

                throw new ArgumentException(msg);
            }

            if (_keyRotationDays <= 0 || _keyRotationDays > 1000)
            {
                var msg = $"{LogPrefix} {KeyRotationInDays} must be an integer greater than 0 and less than or equal to 1,000.";

                Logger.Error(msg);

                throw new ArgumentException(msg);
            }

            _configuration = configuration;

            LoadLastChangedCache();
        }

        private void LoadLastChangedCache()
        {
            if (!File.Exists(_lastChangedCacheFilename))
            {
                return;
            }

            while (true)
            {
                try
                {
                    var json = File.ReadAllText(_lastChangedCacheFilename);
                    var cache = JsonConvert.DeserializeObject<ConcurrentDictionary<string, DateTime>>(json);

                    _lastChangedCache = cache;

                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"{LogPrefix} Error loading last changed cache from {_lastChangedCacheFilename}.");

                    System.Threading.Thread.Sleep(1000);
                }
            }
        }

        private void SaveLastChangedCache()
        {
            while (true)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(_lastChangedCache);
                    File.WriteAllText(_lastChangedCacheFilename, json);

                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"{LogPrefix} Error saving last changed cache from {_lastChangedCacheFilename}.");

                    System.Threading.Thread.Sleep(1000);
                }
            }
        }

        /// <summary>This method is called before the TestVaultConnection() method is called or the Safeguard
        /// Secrets Broker A2A monitor is enabled. The implementation of this method should establish an
        /// authenticated connection with the third-party vault and store the connection in memory to be used
        /// whenever credentials need to be pushed to the vault.</summary>
        /// <param name="credential">The Google Cloud service account credential, in the form of a JSON object,
        /// with an RSA private key used to create self-signed JWT auth tokens.</param>
        public void SetVaultCredential(string credential)
        {
            DebugLog($"{nameof(SetVaultCredential)} being called with: {credential}");

            // https://developers.google.com/identity/protocols/oauth2/service-account
            // See flow chart at the end of the following page, under the "Decision time" section:
            // https://cloud.google.com/blog/products/identity-security/how-to-authenticate-service-accounts-to-help-keep-applications-secure

            _googleServiceAccount = JsonConvert.DeserializeObject<GoogleServiceAccountKey>(credential);

            Logger.Information($"{LogPrefix} {nameof(SetVaultCredential)} was called.");
        }

        /// <summary>This method is called whenever the API /service/devops/v1/Plugins/{name}/TestConnection is
        /// called. The implementation of the method should use the authenticated connection that was established
        /// when the SetVaultCredentials() method was called and test the connectivity to the third-party vault.</summary>
        /// <returns></returns>
        public bool TestVaultConnection()
        {
            DebugLog($"{nameof(TestVaultConnection)} being called.");

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

                Logger.Information($"{LogPrefix} Successfully created auth token for Project Id: {_configuration[ProjectId]}. However, no test connection can be done at this time. Proceed with managing service account key.");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"{LogPrefix} Error while testing the configuration.");
            }

            return false;
        }

        /// <summary>This method is called immediately after the monitor has been enabled, when the Safeguard Secrets
        /// Broker has been notified that a monitored credential changed and when a new credential needs to be pushed
        /// to the corresponding vault. The implementation of this method should use the established connection to the
        /// vault to push the new credential as the specified account name.</summary>
        /// <param name="credentialType"></param>
        /// <param name="asset"></param>
        /// <param name="account"></param>
        /// <param name="credential"></param>
        /// <param name="altAccountName"></param>
        /// <returns></returns>
        public string SetCredential(CredentialType credentialType, string asset, string account, string[] credential, string altAccountName)
        {
            DebugLog($"{nameof(SetCredential)} being called with {nameof(credentialType)}={credentialType} {nameof(asset)}={asset} {nameof(account)}={account} {nameof(credential)}={string.Join(',', credential)} {nameof(altAccountName)}={altAccountName}");

            throw new NotSupportedException("This plug-in can only be used in reverse flow mode.");
        }

        /// <summary>This method is called immediately after the monitor has been enabled and on a polling
        /// schedule that defaults every 60 seconds. This method is not called if the plug-in does not support
        /// the Reverse Flow functionality or the plug-in instance does not have the Reverse Flow functionality
        /// enabled.</summary>
        /// <param name="credentialType"></param>
        /// <param name="asset"></param>
        /// <param name="account"></param>
        /// <param name="altAccountName"></param>
        /// <returns>A new Google Cloud Service Account key, as a string of JSON.</returns>
        public string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName)
        {
            // For reverse flow only.

            DebugLog($"{nameof(GetCredential)} being called with {nameof(credentialType)}={credentialType} {nameof(asset)}={asset} {nameof(account)}={account} {nameof(altAccountName)}={altAccountName}");

            var cacheKey = $"{asset}:{altAccountName ?? account}";

            if (_lastChangedCache.TryGetValue(cacheKey, out var lastChanged) && lastChanged.AddDays(_keyRotationDays) > DateTime.UtcNow)
            {
                // Secrets Broker will log this as an error and then continue on to the next account.
                // Not sure if there is a better way. It will probably get pretty annoying to see an
                // error in the log every 60 seconds, for every account being monitored.
                return null;
            }

            var oldkeys = GetCurrentKeys(asset, account, altAccountName);
            var newkey = CreateNewKey(asset, account, altAccountName);

            if (newkey == null)
            {
                // Failed to create a new key for some reason. Don't delete the old keys. The error
                // has been logged, so we'll just return, which will log another generic error.
                return null;
            }

            // There was a bug in the SafeguardDotNet that Secrets Broker uses, in which it was incorrectly "serializing" a JSON
            // string by just adding quotes around it. It didn't escape out any characters, therefore, if the string itself had
            // a quote, it would fail. But by the time it fails, if we already deleted the previous key, but are unable to save
            // the new one, then we'd end up being locked out of the account.
            //
            // So we need to come up with a way to only delete the old key when we know for sure that Safeguard has saved the new
            // key.
            DeleteOldKeys(oldkeys, asset, account, altAccountName);

            _lastChangedCache[cacheKey] = DateTime.UtcNow;
            SaveLastChangedCache();

            // Secrets Broker will only call SetVaultCredential once for the lifetime of the plug-in.
            // But we want to be able to rotate the service account's key as well. When we do that, we
            // then wouldn't be able to generate a new OAuth token and authenticate. So we are going to
            // have to inspect and keep track of if we ever change the service account's key.
            if (oldkeys.Any(x => x.GetKeyID() == _googleServiceAccount.PrivateKeyId))
            {
                // Any existing OAuth tokens are still valid and can be used when making API calls, even
                // when the key id deleted. The key is only used when generating a new OAuth token, not
                // validating one. So we'll store the new key in memory and when the OAuth token expires,
                // the new key will be used.
                _googleServiceAccount = newkey;
            }

            return JsonConvert.SerializeObject(newkey);
        }

        private List<GoogleServiceAccountKeyResponse> GetCurrentKeys(string asset, string account, string altAccountName)
        {
            // GET https://iam.googleapis.com/v1/projects/PROJECT_ID/serviceAccounts/SA_NAME@PROJECT_ID.iam.gserviceaccount.com/keys?keyTypes=KEY_TYPES
            var path = $"v1/projects/{_configuration[ProjectId]}/serviceAccounts/{altAccountName ?? account}/keys?keyTypes=USER_MANAGED";

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
                Logger.Error($"{LogPrefix} Error getting service account keys for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}.\r\n{content}");

                return new List<GoogleServiceAccountKeyResponse>();
            }

            var keys = response.Content.ReadFromJsonAsync<GoogleServiceAccountKeysResponse>().GetAwaiter().GetResult();

            if (keys == null)
            {
                Logger.Error($"{LogPrefix} Unknown response when getting service account keys for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}.");

                return new List<GoogleServiceAccountKeyResponse>();
            }

            return keys.Keys ?? new List<GoogleServiceAccountKeyResponse>();
        }

        private GoogleServiceAccountKey CreateNewKey(string asset, string account, string altAccountName)
        {
            // https://cloud.google.com/iam/docs/keys-create-delete#allow-creation

            // The account name is expected to be the full:
            // kevinkeyadmin@devtest-oid-rnd-amer-safeguard.iam.gserviceaccount.com
            //
            // https://cloud.google.com/iam/docs/keys-create-delete#creating
            // POST https://iam.googleapis.com/v1/projects/PROJECT_ID/serviceAccounts/SA_NAME@PROJECT_ID.iam.gserviceaccount.com/keys
            var path = $"v1/projects/{_configuration[ProjectId]}/serviceAccounts/{altAccountName ?? account}/keys";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(path, UriKind.Relative),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _googleServiceAccount.GetAuthToken());

            var response = HttpClient.Send(request);

            if (!response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                Logger.Error($"{LogPrefix} Error creating service account key for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}.\r\n{content}");

                return null;
            }

            var key = response.Content.ReadFromJsonAsync<GoogleServiceAccountKeyResponse>().GetAwaiter().GetResult();

            if (key == null)
            {
                Logger.Error($"{LogPrefix} Unknown response when creating service account key for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}.");

                return null;
            }

            return key.ToDto();
        }

        private void DeleteOldKeys(List<GoogleServiceAccountKeyResponse> oldkeys, string asset, string account, string altAccountName)
        {
            // https://cloud.google.com/iam/docs/keys-create-delete#deleting
            // Deleting a service account key does not revoke short-lived credentials that were issued based on
            // the key. To revoke a compromised short-lived credential, you must disable or delete the service
            // account that the credential represents.

            // https://cloud.google.com/iam/docs/keys-disable-enable
            // Disabling a service account key does not revoke short-lived credentials that were issued based on
            // the key. To revoke a compromised short-lived credential, you must disable or delete the service
            // account that the credential represents.
            foreach (var item in oldkeys)
            {
                var keyID = item.GetKeyID();

                // DELETE https://iam.googleapis.com/v1/projects/PROJECT_ID/serviceAccounts/SA_NAME@PROJECT_ID.iam.gserviceaccount.com/keys/KEY_ID
                var path = $"v1/projects/{_configuration[ProjectId]}/serviceAccounts/{altAccountName ?? account}/keys/{keyID}";

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri(path, UriKind.Relative),
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _googleServiceAccount.GetAuthToken());

                var response = HttpClient.Send(request);

                if (!response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Logger.Error($"{LogPrefix} Error deleting service account key {keyID} for Project Id: {_configuration[ProjectId]} Asset: {asset} Account: {altAccountName ?? account}.\r\n{content}");

                    continue;
                }

                // No response data expected.
            }
        }

        /// <summary>This method is called whenever the Safeguard Secrets Broker service is restarted or shutdown.
        /// The implementation of this method should include anything that needs to be done to the plugin to cleanly
        /// shutdown.</summary>
        public void Unload()
        {
            DebugLog($"{nameof(Unload)} being called.");

            // No resources to clean up or do here.
        }

        private void DebugLog(string msg)
        {
            if (_debugLog)
            {
                Logger.Information(msg);
            }
        }
    }

    /// <summary>The response from:
    /// GET https://iam.googleapis.com/v1/projects/PROJECT_ID/serviceAccounts/SA_NAME@PROJECT_ID.iam.gserviceaccount.com/keys?keyTypes=KEY_TYPES
    /// Where all we care about is the key identifier, which is on the end of the <c>name</c> property.</summary>
    /// <example>
    /// {
    ///   "keys": [
    ///     {
    ///       "name": "projects/my-project/serviceAccounts/my-service-account@my-project.iam.gserviceaccount.com/keys/90c48f61c65cd56224a12ab18e6ee9ca9c3aee7c",
    ///       "validAfterTime": "2020-03-04T17:39:47Z",
    ///       "validBeforeTime": "9999-12-31T23:59:59Z",
    ///       "keyAlgorithm": "KEY_ALG_RSA_2048",
    ///       "keyOrigin": "GOOGLE_PROVIDED",
    ///       "keyType": "USER_MANAGED"
    ///     },
    ///     {
    ///       "name": "projects/my-project/serviceAccounts/my-service-account@my-project.iam.gserviceaccount.com/keys/e5e3800831ac1adc8a5849da7d827b4724b1fce8",
    ///       "validAfterTime": "2020-03-31T23:50:09Z",
    ///       "validBeforeTime": "9999-12-31T23:59:59Z",
    ///       "keyAlgorithm": "KEY_ALG_RSA_2048",
    ///       "keyOrigin": "GOOGLE_PROVIDED",
    ///       "keyType": "USER_MANAGED"
    ///     },
    ///     {
    ///       "name": "projects/my-project/serviceAccounts/my-service-account@my-project.iam.gserviceaccount.com/keys/b97699f042b8eee6a846f4f96259fbcd13e2682e",
    ///       "validAfterTime": "2020-05-17T18:58:13Z",
    ///       "validBeforeTime": "9999-12-31T23:59:59Z",
    ///       "keyAlgorithm": "KEY_ALG_RSA_2048",
    ///       "keyOrigin": "GOOGLE_PROVIDED",
    ///       "keyType": "USER_MANAGED",
    ///       "disabled": true
    ///       "disable_reason": "SERVICE_ACCOUNT_KEY_DISABLE_REASON_EXPOSED"
    ///       "extended_status": "SERVICE_ACCOUNT_KEY_EXTENDED_STATUS_KEY_EXPOSED"
    ///       "extended_status_message": "exposed at: https://www.github.com/SomePublicRepo"
    ///     }
    ///   ]
    /// }
    /// </example>
    /// <remarks>All keys will be returned. That is, we will not discriminate between "user managed"
    /// and "system managed" key types. The plug-in will delete all other keys, except for the current.</remarks>
    public class GoogleServiceAccountKeysResponse
    {
        [JsonProperty("keys")]
        public List<GoogleServiceAccountKeyResponse> Keys { get; set; } = new List<GoogleServiceAccountKeyResponse>();
    }

    /// <summary>The response from:<br/>
    /// <c>POST https://iam.googleapis.com/v1/projects/PROJECT_ID/serviceAccounts/SA_NAME@PROJECT_ID.iam.gserviceaccount.com/keys</c><br/>
    /// Seems a little weird that the response you get from this is different than what you get
    /// via the web site. But it turns out that the documentation is a bit misleading in that it
    /// says, "where ENCODED_PRIVATE_KEY is the private portion of the public/private key pair,
    /// encoded in base64." But it turns out, it contains the entire JSON that you get when you
    /// download via the web site, and then Base64 encoded. Therefore, all we'd really need to
    /// care about is deserializing the PrivateKeyData property. Then Base64 decode it into the
    /// <see cref="GoogleServiceAccountKey"/> object. However, we also use this type when getting
    /// the list of keys. And want to be able to parse the <see cref="Name"/> property for the
    /// key ID.</summary>
    /// <example>
    /// {
    ///   "name": "projects/PROJECT_ID/serviceAccounts/SERVICE_ACCOUNT_EMAIL/keys/KEY_ID",
    ///   "privateKeyType": "TYPE_GOOGLE_CREDENTIALS_FILE",
    ///   "privateKeyData": "ENCODED_PRIVATE_KEY",
    ///   "validAfterTime": "DATE",
    ///   "validBeforeTime": "DATE",
    ///   "keyAlgorithm": "KEY_ALG_RSA_2048"
    /// }
    /// </example>
    public class GoogleServiceAccountKeyResponse
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("privateKeyType")]
        public string PrivateKeyType { get; set; }

        [JsonProperty("privateKeyData")]
        public string PrivateKeyData { get; set; }

        [JsonProperty("validAfterTime")]
        public DateTime ValidAfterTime { get; set; }

        [JsonProperty("validBeforeTime")]
        public DateTime ValidBeforeTime { get; set; }

        [JsonProperty("keyAlgorithm")]
        public string KeyAlgorithm { get; set; }

        public GoogleServiceAccountKey ToDto()
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(PrivateKeyData));

            return JsonConvert.DeserializeObject<GoogleServiceAccountKey>(json);
        }

        public string GetKeyID()
        {
            return Name.Split('/').Last();
        }
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
                aud = "https://iam.googleapis.com/",
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