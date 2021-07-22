using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using Microsoft.AspNetCore.WebUtilities;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.DevOps.Extensions;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;

namespace OneIdentity.DevOps.Logic
{
    internal class SafeguardLogic : ISafeguardLogic, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;

        private ServiceConfiguration _serviceConfiguration;
        private DevOpsSecretsBroker _devOpsSecretsBroker;

        public DevOpsSecretsBroker DevOpsSecretsBroker
        {
            get => _devOpsSecretsBroker;
            private set
            {
                _devOpsSecretsBroker = value;
                _configDb.AssetId = _devOpsSecretsBroker?.Asset?.Id;
                _configDb.A2aUserId = _devOpsSecretsBroker?.A2AUser?.Id;
            }
        }

        public SafeguardLogic(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
        }

        bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return CertificateHelper.CertificateValidation(sender, certificate, chain, sslPolicyErrors, _logger,
                _configDb);
        }

        private DevOpsException LogAndException(string msg, Exception ex = null)
        {
            _logger.Error(ex, msg);
            return new DevOpsException(msg, ex);
        }

        private static IDictionary<string, string> AddDevOpsHeader(string devOpsInstanceId,
            IDictionary<string, string> additionalHeaders)
        {
            if (additionalHeaders == null)
                return new Dictionary<string, string> {{"devOpsInstanceId", devOpsInstanceId}};

            additionalHeaders.Add("devOpsInstanceId", devOpsInstanceId);
            return additionalHeaders;
        }

        public static string DevOpsInvokeMethod(string devOpsInstanceId, ISafeguardConnection sgConnection,
            Service service, Method method,
            string relativeUrl, string body = null, IDictionary<string, string> parameters = null,
            IDictionary<string, string> additionalHeaders = null, TimeSpan? timeout = null)
        {
            return sgConnection.InvokeMethod(service, method, relativeUrl, body, parameters,
                AddDevOpsHeader(devOpsInstanceId, additionalHeaders), timeout);
        }

        public static FullResponse DevOpsInvokeMethodFull(string devOpsInstanceId, ISafeguardConnection sgConnection,
            Service service, Method method,
            string relativeUrl, string body = null, IDictionary<string, string> parameters = null,
            IDictionary<string, string> additionalHeaders = null, TimeSpan? timeout = null)
        {
            return sgConnection.InvokeMethodFull(service, method, relativeUrl, body, parameters,
                AddDevOpsHeader(devOpsInstanceId, additionalHeaders), timeout);
        }


        private SafeguardDevOpsConnection GetSafeguardAppliance(ISafeguardConnection sgConnection)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                var availabilityJson = DevOpsInvokeMethod(_configDb.SvcId, sg, Service.Notification, Method.Get,
                    "Status/Availability");
                var applianceAvailability = JsonHelper.DeserializeObject<ApplianceAvailability>(availabilityJson);

                return new SafeguardDevOpsConnection()
                {
                    ApplianceAddress = _configDb.SafeguardAddress,
                    ApplianceId = applianceAvailability.ApplianceId,
                    ApplianceName = applianceAvailability.ApplianceName,
                    ApplianceVersion = applianceAvailability.ApplianceVersion,
                    ApplianceState = applianceAvailability.ApplianceCurrentState,
                    DevOpsInstanceId = _configDb.SvcId,
                    Version = WellKnownData.DevOpsServiceVersion()
                };

            }
            catch (SafeguardDotNetException ex)
            {
                throw new DevOpsException($"Failed to get the appliance information: {ex.Message}");
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }
        }

        private SafeguardDevOpsConnection GetSafeguardAvailability(ISafeguardConnection sgConnection,
            SafeguardDevOpsConnection safeguardConnection)
        {
            var safeguard = GetSafeguardAppliance(sgConnection);
            safeguardConnection.ApplianceId = safeguard.ApplianceId;
            safeguardConnection.ApplianceName = safeguard.ApplianceName;
            safeguardConnection.ApplianceVersion = safeguard.ApplianceVersion;
            safeguardConnection.ApplianceState = safeguard.ApplianceState;
            safeguardConnection.DevOpsInstanceId = _configDb.SvcId;
            safeguardConnection.Version = safeguard.Version;

            return safeguardConnection;
        }

        private bool FetchAndStoreSignatureCertificate(string token, SafeguardDevOpsConnection safeguardConnection)
        {
            var signatureCert = FetchSignatureCertificate(token, safeguardConnection.ApplianceAddress);

            if (signatureCert != null)
            {
                if (ValidateLogin(token, safeguardConnection, signatureCert))
                {
                    _configDb.SigningCertificate = signatureCert;
                    return true;
                }
            }

            return false;
        }

        private string FetchSignatureCertificate(string token, string applianceAddress)
        {
            // Chicken and egg problem here. Fetching and storing the signature certificate is the first
            //  thing that has to happen on a new system.  We can't check the SSL certificate unless a certificate
            //  chain has been provided.  However a certificate chain can't be provided until after login but
            //  login can't happen until we have the signature certificate.  So we will just ignore the
            //  the validation of the SSL certificate.
            HttpClientHandler handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, certChain, policyErrors) => true
            };

            using var client = new HttpClient(handler) {BaseAddress = new Uri($"https://{applianceAddress}")};
            var response = client.GetAsync("RSTS/SigningCertificate").Result;
            response.EnsureSuccessStatusCode();

            return CertificateHelper.ConvertPemToBase64(response.Content.ReadAsStringAsync().Result);
        }

        private SafeguardDevOpsConnection ConnectAnonymous(string safeguardAddress, int apiVersion, bool ignoreSsl)
        {
            ISafeguardConnection sg = null;
            try
            {
                var safeguardConnection = new SafeguardDevOpsConnection
                {
                    ApplianceAddress = safeguardAddress,
                    IgnoreSsl = _configDb.IgnoreSsl ?? ignoreSsl,
                    ApiVersion = apiVersion
                };

                sg = ignoreSsl
                    ? Safeguard.Connect(safeguardAddress, apiVersion, true)
                    : Safeguard.Connect(safeguardAddress, CertificateValidationCallback, apiVersion);
                return GetSafeguardAvailability(sg, safeguardConnection);
            }
            catch (SafeguardDotNetException ex)
            {
                throw LogAndException($"Failed to contact Safeguard at '{safeguardAddress}': {ex.Message}", ex);
            }
            finally
            {
                sg?.Dispose();
            }
        }

        private ISafeguardConnection ConnectWithAccessToken(string token, SafeguardDevOpsConnection safeguardConnection)
        {
            if (_serviceConfiguration != null)
            {
                DisconnectWithAccessToken();
            }

            if (string.IsNullOrEmpty(safeguardConnection.ApplianceAddress))
                throw new DevOpsException("Missing safeguard appliance configuration.");
            if (string.IsNullOrEmpty(token))
                throw new DevOpsException("Missing safeguard access token.");

            _serviceConfiguration = new ServiceConfiguration
            {
                AccessToken = token.ToSecureString()
            };

            return Connect(safeguardConnection.ApplianceAddress, token.ToSecureString(), safeguardConnection.ApiVersion,
                safeguardConnection.IgnoreSsl);
        }

        private void DisconnectWithAccessToken()
        {
            _serviceConfiguration?.AccessToken?.Dispose();
            _serviceConfiguration = null;
        }

        private bool GetAndValidateUserPermissions(string token, SafeguardDevOpsConnection safeguardConnection)
        {
            ISafeguardConnection sg = null;
            try
            {
                sg = ConnectWithAccessToken(token, safeguardConnection);

                var meJson = sg.InvokeMethod(Service.Core, Method.Get, "Me");
                var loggedInUser = JsonHelper.DeserializeObject<LoggedInUser>(meJson);

                var valid = loggedInUser.AdminRoles.Any(x => x.Equals("ApplianceAdmin") || x.Equals("OperationsAdmin"));
                if (valid)
                {
                    _serviceConfiguration.Appliance = GetSafeguardAvailability(sg, safeguardConnection);

                    AuthorizedCache.Instance.Add(new ServiceConfiguration(loggedInUser)
                    {
                        AccessToken = token.ToSecureString(),
                        Appliance = _serviceConfiguration.Appliance
                    });
                }

                return valid;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get the user information: {ex.Message}");
            }
            finally
            {
                sg?.Dispose();
            }

            return false;
        }

        private void CreateA2AUser(ISafeguardConnection sg)
        {
            string thumbprint = _configDb.UserCertificate?.Thumbprint;

            if (thumbprint == null)
                throw new DevOpsException("Failed to create A2A user due to missing client certificate");

            var a2aUser = GetA2AUser(sg);
            if (a2aUser == null)
            {
                a2aUser = new A2AUser()
                {
                    UserName = WellKnownData.DevOpsUserName(_configDb.SvcId),
                    PrimaryAuthenticationIdentity = thumbprint
                };

                var a2aUserStr = JsonHelper.SerializeObject(a2aUser);
                try
                {
                    var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Post, "Users", a2aUserStr);
                    if (result.StatusCode == HttpStatusCode.Created)
                    {
                        a2aUser = JsonHelper.DeserializeObject<A2AUser>(result.Body);
                        if (DevOpsSecretsBroker?.A2AUser == null ||
                            DevOpsSecretsBroker.A2AUser.Id != a2aUser.Id)
                        {
                            DevOpsSecretsBroker.A2AUser = a2aUser;
                            UpdateSecretsBrokerInstance(sg, DevOpsSecretsBroker);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndException($"Failed to create the A2A user: {ex.Message}", ex);
                }
            }
            else
            {
                if (!a2aUser.PrimaryAuthenticationIdentity.Equals(thumbprint,
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        a2aUser.PrimaryAuthenticationIdentity = thumbprint;
                        var a2aUserStr = JsonHelper.SerializeObject(a2aUser);
                        var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Put, $"Users/{a2aUser.Id}", a2aUserStr);
                        if (result.StatusCode == HttpStatusCode.OK)
                        {
                            a2aUser = JsonHelper.DeserializeObject<A2AUser>(result.Body);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw LogAndException($"Failed to update the A2A user: {ex.Message}", ex);
                    }
                }

                if (DevOpsSecretsBroker?.A2AUser == null ||
                    DevOpsSecretsBroker.A2AUser.Id != a2aUser.Id)
                {
                    DevOpsSecretsBroker.A2AUser = a2aUser;
                    UpdateSecretsBrokerInstance(sg, DevOpsSecretsBroker);
                }
            }
        }

        private A2AUser GetA2AUser(ISafeguardConnection sgConnection)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                FullResponse result;

                // If we don't have a user Id then try to find the user by name
                if (_configDb.A2aUserId == null)
                {
                    var p = new Dictionary<string, string>
                        {{"filter", $"UserName eq '{WellKnownData.DevOpsUserName(_configDb.SvcId)}'"}};

                    try
                    {
                        result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, 
                            "Users", null, p);
                        if (result.StatusCode == HttpStatusCode.OK)
                        {
                            var foundUsers = JsonHelper.DeserializeObject<List<A2AUser>>(result.Body);

                            if (foundUsers.Count > 0)
                            {
                                var a2aUser = foundUsers.FirstOrDefault();
                                if (a2aUser != null && _configDb.A2aUserId != a2aUser.Id)
                                {
                                    _configDb.A2aUserId = a2aUser.Id;
                                    DevOpsSecretsBroker.A2AUser = a2aUser;
                                }

                                return a2aUser;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to get the A2A user by name: {ex.Message}");
                    }
                }
                else // Otherwise just get the user by id
                {
                    try
                    {
                        var a2aUser = GetA2AUser(sg, _configDb.A2aUserId);
                        if (a2aUser != null)
                        {
                            return a2aUser;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to get the A2A user by id {_configDb.A2aUserId}: {ex.Message}");
                    }

                    // Apparently the user id we have is wrong so get rid of it.
                    _configDb.A2aUserId = null;
                }

                return null;
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }
        }

        private A2AUser GetA2AUser(ISafeguardConnection sg, int? id)
        {
            if (id != null)
            {
                try
                {
                    var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get,
                        $"Users/{id.Value}");
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        return JsonHelper.DeserializeObject<A2AUser>(result.Body);
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndException($"Failed to get the A2A user by id {id.Value}: {ex.Message}");
                }
            }

            return null;
        }

        private void EnableA2AService(ISafeguardConnection sg)
        {
            try
            {
                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Appliance, Method.Post,
                    "A2AService/Enable");
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Error("Failed to start the A2A service.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to start the A2A service: {ex.Message}");
            }
        }

        private void CreateA2ARegistration(ISafeguardConnection sgConnection, A2ARegistrationType registrationType)
        {
            if (_configDb.A2aUserId == null)
                throw new DevOpsException("Failed to create A2A registration due to missing A2A user");

            var sg = sgConnection ?? Connect();

            try
            {
                var a2aRegistration = GetA2ARegistration(sg, registrationType);
                if (a2aRegistration == null)
                {
                    var registration = new A2ARegistration()
                    {
                        AppName = registrationType == A2ARegistrationType.Account
                            ? WellKnownData.DevOpsRegistrationName(_configDb.SvcId)
                            : WellKnownData.DevOpsVaultRegistrationName(_configDb.SvcId),
                        CertificateUserId = _configDb.A2aUserId.Value,
                        VisibleToCertificateUsers = true,
                        DevOpsInstanceId = _configDb.SvcId
                    };

                    var registrationStr = JsonHelper.SerializeObject(registration);

                    try
                    {
                        var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Post,
                            "A2ARegistrations", registrationStr);
                        if (result.StatusCode == HttpStatusCode.Created)
                        {
                            registration = JsonHelper.DeserializeObject<A2ARegistration>(result.Body);
                            if (registrationType == A2ARegistrationType.Account)
                            {
                                _configDb.A2aRegistrationId = registration.Id;
                            }
                            else
                            {
                                _configDb.A2aVaultRegistrationId = registration.Id;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw LogAndException($"Failed to create the A2A registration: {ex.Message}", ex);
                    }

                    DevOpsSecretsBroker.A2ARegistration = new A2ARegistration()
                    {
                        Id = registration.Id
                    };

                    UpdateSecretsBrokerInstance(sg, DevOpsSecretsBroker);
                }
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }
        }

        public A2ARegistration GetA2ARegistration(ISafeguardConnection sgConnection, A2ARegistrationType registrationType)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                FullResponse result;

                // If we don't have a registration Id then try to find the registration by name
                if ((registrationType == A2ARegistrationType.Account && _configDb.A2aRegistrationId == null) ||
                    (registrationType == A2ARegistrationType.Vault && _configDb.A2aVaultRegistrationId == null))
                {
                    var knownRegistrationName = (registrationType == A2ARegistrationType.Account)
                        ? WellKnownData.DevOpsRegistrationName(_configDb.SvcId)
                        : WellKnownData.DevOpsVaultRegistrationName(_configDb.SvcId);
                    try
                    {
                        var p = new Dictionary<string, string>
                            {{"filter", $"AppName eq '{knownRegistrationName}'"}};

                        result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get,
                            "A2ARegistrations", null, p);
                        if (result.StatusCode == HttpStatusCode.OK)
                        {
                            var foundRegistrations = JsonHelper.DeserializeObject<List<A2ARegistration>>(result.Body);

                            if (foundRegistrations.Count > 0)
                            {
                                var registration = foundRegistrations.FirstOrDefault();
                                if (registration != null)
                                {
                                    if (registrationType == A2ARegistrationType.Account)
                                    {
                                        _configDb.A2aRegistrationId = registration.Id;
                                    }
                                    else
                                    {
                                        _configDb.A2aVaultRegistrationId = registration.Id;
                                    }
                                }

                                return registration;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to get the A2A user by name {knownRegistrationName}: {ex.Message}");
                    }
                }
                else // Otherwise just get the registration by id
                {
                    var registrationId = (registrationType == A2ARegistrationType.Account)
                        ? _configDb.A2aRegistrationId
                        : _configDb.A2aVaultRegistrationId;
                    try
                    {
                        var a2aRegistration = GetA2ARegistration(sg, registrationId);
                        if (a2aRegistration != null)
                        {
                            return a2aRegistration;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Just log the exception and set the registration id in the databsae to null.  If there is a
                        //  registration that matches, it will find it the next time.
                        _logger.Error(ex, $"Failed to get the registration for id '{registrationId}': {ex.Message}");
                    }
                }

                // Apparently the registration id we have is wrong so get rid of it.
                if (registrationType == A2ARegistrationType.Account)
                {
                    _configDb.A2aRegistrationId = null;
                }
                else
                {
                    _configDb.A2aVaultRegistrationId = null;
                }
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return null;
        }

        private A2ARegistration GetA2ARegistration(ISafeguardConnection sg, int? id)
        {
            FullResponse result;

            if (id != null)
            {
                try
                {
                    result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, $"A2ARegistrations/{id}");
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        return JsonHelper.DeserializeObject<A2ARegistration>(result.Body);
                    }
                }
                catch (SafeguardDotNetException ex)
                {
                    if (ex.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.Error($"Registration not found for id '{id}'", ex);
                    }
                    else
                    {
                        throw LogAndException($"Failed to get the registration for id '{id}'", ex);
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndException($"Failed to get the registration for id '{id}'", ex);
                }
            }

            return null;
        }

        private string LocalIPAddress()
        {

            var addresses = GetLocalIPAddresses();
            if (addresses != null)
            {
                var hostAddresses = string.Join(",", addresses.Select(x => $"\"{x}\""));
                _logger.Debug($"Found host IP address(s) {hostAddresses}");

                return hostAddresses;
            }

            return null;
        }

        private List<IPAddress> GetLocalIPAddresses()
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                return null;
            }

            if (bool.Parse(Environment.GetEnvironmentVariable("DOCKER_RUNNING") ?? "false"))
            {
                _logger.Debug("Running in Docker and searching for local IP address");
                var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST_IP") ?? "";
                _logger.Debug($"Found host IP address {dockerHost}");

                try
                {
                    var address = IPAddress.Parse(dockerHost);
                    return new List<IPAddress>()
                    {
                        address
                    };
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Failed to find or convert the IP address from the environment.", ex);
                    return null;
                }
            }

            _logger.Debug("Running as a service and searching for local IP address");
            var host = Dns.GetHostEntry(Dns.GetHostName());

            var addresses = host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            return addresses.ToList();
        }

        public void RemoveClientCertificate()
        {
            _configDb.UserCertificateBase64Data = null;
            _configDb.UserCertificatePassphrase = null;
            _configDb.UserCertificateThumbprint = null;
        }

        public void RemoveWebServerCertificate()
        {
            _configDb.WebSslCertificateBase64Data = null;
            _configDb.WebSslCertificatePassphrase = null;
            _configDb.WebSslCertificate = CertificateHelper.CreateDefaultSslCertificate();
        }

        public bool IsLoggedIn()
        {
            return _serviceConfiguration != null;
        }

        public ISafeguardConnection Connect()
        {
            if (!IsLoggedIn())
                throw new DevOpsException("Not logged in");

            return Connect(_serviceConfiguration.Appliance.ApplianceAddress,
                _serviceConfiguration.AccessToken,
                _serviceConfiguration.Appliance.ApiVersion,
                _serviceConfiguration.Appliance.IgnoreSsl);
        }

        private ISafeguardConnection Connect(string address, SecureString token, int? version, bool? ignoreSsl)
        {
            if (!IsLoggedIn())
            {
                throw new DevOpsException("Not logged in");
            }

            try
            {
                _logger.Debug("Connecting to Safeguard: {address}");
                return (ignoreSsl.HasValue && ignoreSsl.Value)
                    ? Safeguard.Connect(address, token, version ?? WellKnownData.DefaultApiVersion, true)
                    : Safeguard.Connect(address, token, CertificateValidationCallback, version ?? WellKnownData.DefaultApiVersion);
            }
            catch (SafeguardDotNetException ex)
            {
                throw LogAndException($"Failed to connect to Safeguard at '{address}': {ex.Message}", ex);
            }
        }

        public ISafeguardConnection CertConnect()
        {
            var sppAddress = _configDb.SafeguardAddress;
            var userCertificate = _configDb.UserCertificateBase64Data;
            var passPhrase = _configDb.UserCertificatePassphrase?.ToSecureString();
            var apiVersion = _configDb.ApiVersion ?? WellKnownData.DefaultApiVersion;
            var ignoreSsl = _configDb.IgnoreSsl ?? true;

            if (sppAddress != null && userCertificate != null)
            {
                try
                {
                    _logger.Debug("Connecting to Safeguard: {address}");
                    var connection = Safeguard.Connect(sppAddress, Convert.FromBase64String(userCertificate),
                        passPhrase, apiVersion, ignoreSsl);
                    return connection;
                }
                catch (SafeguardDotNetException ex)
                {
                    _logger.Error(ex, $"Failed to connect to Safeguard at '{sppAddress}': {ex.Message}");
                }
            }

            return null;
        }

        public bool ValidateLogin(string token, bool tokenOnly = false)
        {

            return ValidateLogin(token, tokenOnly
                ? null
                : new SafeguardDevOpsConnection()
                {
                    ApiVersion = _configDb.ApiVersion ?? WellKnownData.DefaultApiVersion,
                    ApplianceAddress = _configDb.SafeguardAddress,
                    IgnoreSsl = _configDb.IgnoreSsl ?? false
                });
        }

        private bool ValidateLogin(string token, SafeguardDevOpsConnection safeguardConnection = null, string tempKey = null)
        {
            if (token == null)
                return false;

            try
            {
                var key = tempKey ?? _configDb.SigningCertificate;
                var bytes = Convert.FromBase64String(key);
                var cert = new X509Certificate2(bytes);

                var parts = token.Split('.');

                var header = parts[0];
                var payload = parts[1];
                var signature = Base64UrlTextEncoder.Decode(parts[2]);
                var data = Encoding.UTF8.GetBytes(header + '.' + payload);

                var validToken = cert.GetRSAPublicKey()
                    .VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (validToken && safeguardConnection != null)
                {
                    return GetAndValidateUserPermissions(token, safeguardConnection);
                }

                return validToken;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }

            return false;
        }

        public CertificateInfo GetCertificateInfo(CertificateType certificateType)
        {
            var cert = certificateType == CertificateType.A2AClient
                ? _configDb.UserCertificate
                : _configDb.WebSslCertificate;

            if (cert != null)
            {
                var result = new CertificateInfo()
                {
                    Thumbprint = cert.Thumbprint,
                    IssuedBy = cert.Issuer,
                    Subject = cert.Subject,
                    NotAfter = cert.NotAfter,
                    NotBefore = cert.NotBefore,
                    Base64CertificateData = cert.ToPemFormat()
                };
                return result;
            }

            var msg = $"{Enum.GetName(typeof(CertificateType), certificateType)} certificate not found.";
            _logger.Error(msg);
            throw new DevOpsException(msg, HttpStatusCode.NotFound);
        }

        public void InstallCertificate(CertificateInfo certificate, CertificateType certificateType)
        {
            var certificateBytes = CertificateHelper.ConvertPemToData(certificate.Base64CertificateData);
            X509Certificate2 cert;
            try
            {
                cert = certificate.Passphrase == null
                    ? new X509Certificate2(certificateBytes)
                    : new X509Certificate2(certificateBytes, certificate.Passphrase);
                _logger.Debug(
                    $"Parsed certificate for installation: subject={cert.SubjectName.Name}, thumbprint={cert.Thumbprint}");
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to convert the provided certificate: {ex.Message}", ex);
            }

            if (cert.HasPrivateKey)
            {
                _logger.Debug("Parsed certificate contains private key");
                if (!CertificateHelper.ValidateCertificate(cert, certificateType))
                    throw new DevOpsException("Invalid certificate");

                switch (certificateType)
                {
                    case CertificateType.A2AClient:
                    {
                        _configDb.UserCertificatePassphrase = certificate.Passphrase;
                        _configDb.UserCertificateBase64Data = certificate.Base64CertificateData;
                        break;
                    }
                    case CertificateType.WebSsl:
                    {
                        _configDb.WebSslCertificatePassphrase = certificate.Passphrase;
                        _configDb.WebSslCertificateBase64Data = certificate.Base64CertificateData;
                        break;
                    }
                    default:
                    {
                        throw new DevOpsException("Invalid certificate type");
                    }
                }
            }
            else
            {
                try
                {
                    using var rsa = RSA.Create();
                    var privateKeyBytesBase64 = certificateType == CertificateType.A2AClient
                        ? _configDb.UserCsrPrivateKeyBase64Data
                        : _configDb.WebSslCsrPrivateKeyBase64Data;
                    if (privateKeyBytesBase64 == null)
                    {
                        throw LogAndException(
                            "Failed to find a matching private key. Possibly a mismatched CSR type was selected.");
                    }

                    var privateKeyBytes = Convert.FromBase64String(privateKeyBytesBase64);
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

                    using (X509Certificate2 pubOnly = cert)
                    using (X509Certificate2 pubPrivEphemeral = pubOnly.CopyWithPrivateKey(rsa))
                    {
                        if (!CertificateHelper.ValidateCertificate(pubPrivEphemeral, certificateType))
                            throw new DevOpsException("Invalid certificate");

                        switch (certificateType)
                        {
                            case CertificateType.A2AClient:
                            {
                                _configDb.UserCertificatePassphrase = null;
                                _configDb.UserCertificateBase64Data =
                                    Convert.ToBase64String(pubPrivEphemeral.Export(X509ContentType.Pfx));
                                break;
                            }
                            case CertificateType.WebSsl:
                            {
                                _configDb.WebSslCertificatePassphrase = null;
                                _configDb.WebSslCertificateBase64Data =
                                    Convert.ToBase64String(pubPrivEphemeral.Export(X509ContentType.Pfx));
                                break;
                            }
                            default:
                            {
                                throw new DevOpsException("Invalid certificate type");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndException($"Failed to import the certificate: {ex.Message}", ex);
                }
            }
        }

        public string GetCSR(int? size, string subjectName, string sanDns, string sanIp, CertificateType certificateType)
        {
            var certSize = 2048;
            var certSubjectName = certificateType == CertificateType.A2AClient
                ? WellKnownData.DevOpsServiceClientCertificate(_configDb.SvcId)
                : WellKnownData.DevOpsServiceWebSslCertificate(_configDb.SvcId);

            if (size != null)
                certSize = size.Value;
            if (subjectName != null)
            {
                try
                {
                    subjectName = subjectName.Trim('"').Trim('\'');
                    var validCnName = new X500DistinguishedName(subjectName);
                    certSubjectName = validCnName.Name;
                }
                catch (Exception ex)
                {
                    throw LogAndException("Invalid subject name.", ex);
                }
            }

            using (RSA rsa = RSA.Create(certSize))
            {
                var certificateRequest = new CertificateRequest(certSubjectName, rsa,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                certificateRequest.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false));

                switch (certificateType)
                {
                    case CertificateType.A2AClient:
                    {
                        certificateRequest.CertificateExtensions.Add(
                            new X509KeyUsageExtension(
                                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement | X509KeyUsageFlags.KeyEncipherment,
                                true));
                        certificateRequest.CertificateExtensions.Add(
                            new X509EnhancedKeyUsageExtension(
                                new OidCollection
                                {
                                    new Oid("1.3.6.1.5.5.7.3.2")
                                },
                                false));
                        break;
                    }
                    case CertificateType.WebSsl:
                    {
                        certificateRequest.CertificateExtensions.Add(
                            new X509KeyUsageExtension(
                                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement | X509KeyUsageFlags.KeyEncipherment,
                                true));
                        certificateRequest.CertificateExtensions.Add(
                            new X509EnhancedKeyUsageExtension(
                                new OidCollection
                                {
                                    new Oid("1.3.6.1.5.5.7.3.1")
                                },
                                false));
                        break;
                    }
                    default:
                    {
                        throw new DevOpsException("Invalid certificate type");
                    }
                }

                certificateRequest.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false));

                var sanBuilder = new SubjectAlternativeNameBuilder();
                if (sanIp != null)
                {
                    try
                    {
                        var addresses = sanIp.Split(',').ToList();
                        addresses.ForEach(x => sanBuilder.AddIpAddress(IPAddress.Parse(x)));
                    }
                    catch (Exception ex)
                    {
                        throw LogAndException("Invalid SAN IP address list.", ex);
                    }
                }

                if (sanDns != null)
                {
                    try
                    {
                        var dnsNames = sanDns.Split(',').ToList();
                        dnsNames.ForEach(x => sanBuilder.AddDnsName(x.Trim()));
                    }
                    catch (Exception ex)
                    {
                        throw LogAndException("Invalid SAN DNS list.", ex);
                    }
                }

                certificateRequest.CertificateExtensions.Add(sanBuilder.Build());

                var csr = certificateRequest.CreateSigningRequest();
                switch (certificateType)
                {
                    case CertificateType.A2AClient:
                    {
                        _configDb.UserCsrBase64Data = Convert.ToBase64String(csr);
                        _configDb.UserCsrPrivateKeyBase64Data = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
                        break;
                    }
                    case CertificateType.WebSsl:
                    {
                        _configDb.WebSslCsrBase64Data = Convert.ToBase64String(csr);
                        _configDb.WebSslCsrPrivateKeyBase64Data = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
                        break;
                    }
                    default:
                    {
                        throw new DevOpsException("Invalid certificate type");
                    }
                }

                var builder = new StringBuilder();
                builder.Append("-----BEGIN CERTIFICATE REQUEST-----\r\n");
                builder.Append(PemExtensions.AddPemLineBreaks(Convert.ToBase64String(csr)));
                builder.Append("\r\n-----END CERTIFICATE REQUEST-----");

                return builder.ToString();
            }
        }

        public ServiceConfiguration ConfigureDevOpsService()
        {
            using var sg = Connect();
            CreateA2AUser(sg);
            CreateA2ARegistration(sg, A2ARegistrationType.Account);
            CreateA2ARegistration(sg, A2ARegistrationType.Vault);
            EnableA2AService(sg);

            return GetDevOpsConfiguration(sg);
        }

        public SafeguardDevOpsConnection GetAnonymousSafeguardConnection()
        {
            if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                return new SafeguardDevOpsConnection();

            ISafeguardConnection sg = null;
            try
            {
                var safeguardConnection = new SafeguardDevOpsConnection
                {
                    ApplianceAddress = _configDb.SafeguardAddress,
                    IgnoreSsl = _configDb.IgnoreSsl,
                    ApiVersion = _configDb.ApiVersion ?? WellKnownData.DefaultApiVersion
                };

                sg = Safeguard.Connect(safeguardConnection.ApplianceAddress,
                    safeguardConnection.ApiVersion ?? WellKnownData.DefaultApiVersion, true);
                return GetSafeguardAvailability(sg, safeguardConnection);
            }
            catch (SafeguardDotNetException ex)
            {
                throw LogAndException($"Failed to contact Safeguard at '{_configDb.SafeguardAddress}': {ex.Message}",
                    ex);
            }
            finally
            {
                sg?.Dispose();
            }
        }

        public SafeguardDevOpsConnection GetSafeguardConnection()
        {
            if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                return new SafeguardDevOpsConnection();

            return ConnectAnonymous(_configDb.SafeguardAddress, _configDb.ApiVersion ?? WellKnownData.DefaultApiVersion, true);
        }

        public void RetrieveDevOpsSecretsBrokerInstance(ISafeguardConnection sgConnection)
        {
            var sg = sgConnection ?? Connect();

            try {
                var secretsBroker = GetSecretsBrokerInstanceByName(sg);
                if (secretsBroker != null)
                {
                    DevOpsSecretsBroker = secretsBroker;
                }
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

        }

        public SafeguardDevOpsConnection SetSafeguardData(string token, SafeguardData safeguardData)
        {
            if (token == null)
                throw new DevOpsException("Invalid authorization token.", HttpStatusCode.Unauthorized);

            if (_configDb.SafeguardAddress != null)
            {
                var signatureCert = FetchSignatureCertificate(token, _configDb.SafeguardAddress);
                if (!ValidateLogin(token, null, signatureCert))
                {
                    if (_configDb.SafeguardAddress.Equals(safeguardData.ApplianceAddress))
                    {
                        throw new DevOpsException("Authorization Failed: Invalid token", HttpStatusCode.Unauthorized);
                    }

                    throw LogAndException(
                        "Invalid token. The previously configured Secrets Broker cannot be repurposed until the configuration is deleted.");
                }
            }

            if (safeguardData.IgnoreSsl.HasValue && !safeguardData.IgnoreSsl.Value &&
                !_configDb.GetAllTrustedCertificates().Any())
            {
                throw LogAndException("Cannot ignore SSL before adding trusted certificates.");
            }

            var safeguardConnection = ConnectAnonymous(safeguardData.ApplianceAddress,
                safeguardData.ApiVersion ?? WellKnownData.DefaultApiVersion, safeguardData.IgnoreSsl ?? false);

            if (safeguardConnection != null && FetchAndStoreSignatureCertificate(token, safeguardConnection))
            {
                _configDb.SafeguardAddress = safeguardData.ApplianceAddress;
                _configDb.ApiVersion = safeguardData.ApiVersion ?? WellKnownData.DefaultApiVersion;
                _configDb.IgnoreSsl = safeguardData.IgnoreSsl ?? false;

                safeguardConnection.ApplianceAddress = _configDb.SafeguardAddress;
                safeguardConnection.ApiVersion = _configDb.ApiVersion;
                safeguardConnection.IgnoreSsl = _configDb.IgnoreSsl;
                return safeguardConnection;
            }

            throw LogAndException(
                $"Invalid authorization token or SPP appliance {safeguardData.ApplianceAddress} is unavailable.");
        }

        public void DeleteSafeguardData()
        {
            // Since the database is about to be dropped, we can't store deleted plugins in the database.
            // Write an empty file in the plugins dir to indicate on startup that the plugins need to be deleted.
            if (Directory.Exists(WellKnownData.PluginDirPath))
            {
                File.Create(WellKnownData.DeleteAllPlugins).Dispose();
            }

            _configDb.DropDatabase();
            _configDb.SvcId = WellKnownData.ServiceIdentitifierRegenerate;
            RestartService();
        }

        public void DeleteDevOpsConfiguration(ISafeguardConnection sgConnection, IAddonLogic addonLogic, bool secretsBrokerOnly)
        {
            var sg = sgConnection ?? Connect();

            foreach (var addon in _configDb.GetAllAddons())
            {
                addonLogic.RemoveAddon(addon.Name);
            }

            if (!secretsBrokerOnly)
            {
                DeleteDevOpsInstance(sg);
            }

            DeleteSafeguardData();
            DevOpsSecretsBroker = null;
            DisconnectWithAccessToken();
        }

        private void DeleteDevOpsInstance(ISafeguardConnection sgConnection)
        {
            try
            {
                var devOpsInstance = GetSecretsBrokerInstance(sgConnection);
                if (devOpsInstance != null)
                {
                    var result = DevOpsInvokeMethodFull(_configDb.SvcId, sgConnection, Service.Core, Method.Delete,
                        $"DevOps/SecretsBrokers/{devOpsInstance.Id}");
                    if (result.StatusCode != HttpStatusCode.NoContent)
                    {
                        var errorMessage = JsonHelper.DeserializeObject<ErrorMessage>(result.Body);
                        throw LogAndException(
                            $"Failed to delete the DevOps Secrets Broker Instance. {errorMessage.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw LogAndException("Failed to delete the DevOps Secrets Broker Instance.", ex);
            }
        }

        public void RestartService()
        {
            // Sleep just for a second to give the caller time to respond before we exit.
            Thread.Sleep(1000);
            Task.Run(() => Environment.Exit(54));
        }

        public IEnumerable<CertificateInfo> GetTrustedCertificates()
        {
            var trustedCertificates = _configDb.GetAllTrustedCertificates().ToArray();
            if (!trustedCertificates.Any())
                return new List<CertificateInfo>();

            return trustedCertificates.Select(x => x.GetCertificateInfo());
        }

        public CertificateInfo GetTrustedCertificate(string thumbPrint)
        {
            if (string.IsNullOrEmpty(thumbPrint))
                throw LogAndException("Invalid thumbprint");

            var certificate = _configDb.GetTrustedCertificateByThumbPrint(thumbPrint);

            return certificate?.GetCertificateInfo();
        }

        private CertificateInfo AddTrustedCertificate(string base64CertificateData)
        {
            if (base64CertificateData == null)
                throw LogAndException("Certificate cannot be null");

            try
            {
                var certificateBytes = CertificateHelper.ConvertPemToData(base64CertificateData);
                var cert = new X509Certificate2(certificateBytes);
                _logger.Debug(
                    $"Parsed new trusted certificate: subject={cert.SubjectName}, thumbprint={cert.Thumbprint}.");

                // Check of the certificate already exists and just return it if it does.
                var existingCert = _configDb.GetTrustedCertificateByThumbPrint(cert.Thumbprint);
                if (existingCert != null)
                {
                    _logger.Debug("New trusted certificate already exists.");
                    return existingCert.GetCertificateInfo();
                }

                if (!CertificateHelper.ValidateCertificate(cert, CertificateType.Trusted))
                    throw new DevOpsException("Invalid certificate");

                var trustedCertificate = new TrustedCertificate()
                {
                    Thumbprint = cert.Thumbprint,
                    Base64CertificateData = cert.ToPemFormat()
                };

                _configDb.SaveTrustedCertificate(trustedCertificate);
                return trustedCertificate.GetCertificateInfo();
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to add the certificate: {ex.Message}", ex);
            }
        }

        public CertificateInfo AddTrustedCertificate(CertificateInfo certificate)
        {
            return AddTrustedCertificate(certificate.Base64CertificateData);
        }

        public void DeleteTrustedCertificate(string thumbPrint)
        {
            if (string.IsNullOrEmpty(thumbPrint))
                throw LogAndException("Invalid thumbprint");

            _configDb.DeleteTrustedCertificateByThumbPrint(thumbPrint);
        }

        public IEnumerable<CertificateInfo> ImportTrustedCertificates(ISafeguardConnection sgConnection)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                IEnumerable<ServerCertificate> serverCertificates = null;
                var trustedCertificates = new List<CertificateInfo>();

                try
                {

                    var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, "TrustedCertificates");
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        serverCertificates = JsonHelper.DeserializeObject<IEnumerable<ServerCertificate>>(result.Body);
                        _logger.Debug($"Received {serverCertificates.Count()} certificates from Safeguard.");
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndException("Failed to get the Safeguard trusted certificates.", ex);
                }

                if (serverCertificates != null)
                {
                    foreach (var cert in serverCertificates)
                    {
                        try
                        {
                            _logger.Debug($"Importing trusted certificate {cert.Subject} {cert.Thumbprint}.");
                            var certificateInfo = AddTrustedCertificate(cert.Base64CertificateData);
                            trustedCertificates.Add(certificateInfo);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex,
                                $"Failed to import certificate {cert.Subject} {cert.Thumbprint} from Safeguard. {ex.Message}");
                        }
                    }
                }

                return trustedCertificates;
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }
        }

        public void DeleteAllTrustedCertificates()
        {
            _configDb.DeleteAllTrustedCertificates();
        }

        private DevOpsSecretsBroker GetSecretsBrokerInstance(ISafeguardConnection sg)
        {
            try
            {
                if (DevOpsSecretsBroker == null)
                {
                    return GetSecretsBrokerInstanceByName(sg);
                }

                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get,
                    $"DevOps/SecretsBrokers/{DevOpsSecretsBroker.Id}");

                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var secretsBroker = JsonHelper.DeserializeObject<DevOpsSecretsBroker>(result.Body);
                    if (secretsBroker != null)
                    {
                        return secretsBroker;
                    }
                }

                _logger.Error("Failed to get the DevOps Secrets Broker instance from Safeguard.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get the DevOps Secrets Broker instance from Safeguard. ");
            }

            return null;
        }

        private DevOpsSecretsBroker GetSecretsBrokerInstanceByName(ISafeguardConnection sg) 
        {
            try
            {
                var filter = $"DevOpsInstanceId eq \"{_configDb.SvcId}\"";

                var p = new Dictionary<string, string>();
                JsonHelper.AddQueryParameter(p, nameof(filter), filter);

                var result =
                    DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, "DevOps/SecretsBrokers", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var secretsBroker =
                        (JsonHelper.DeserializeObject<IEnumerable<DevOpsSecretsBroker>>(result.Body)).FirstOrDefault();
                    if (secretsBroker != null)
                    {
                        return secretsBroker;
                    }
                }
            }
            catch (SafeguardDotNetException ex)
            {
                if (ex.HttpStatusCode != HttpStatusCode.NotFound)
                    _logger.Error(ex, "Failed to get the DevOps Secrets Broker instance from Safeguard. ");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get the DevOps Secrets Broker instance from Safeguard. ");
            }

            return null;
        }

        public List<DevOpsSecretsBrokerAccount> GetSecretsBrokerAccounts(ISafeguardConnection sg)
        {
            try
            {
                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, $"DevOps/SecretsBrokers/{DevOpsSecretsBroker.Id}/Accounts");
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var secretsBrokerAccounts = JsonHelper.DeserializeObject<IEnumerable<DevOpsSecretsBrokerAccount>>(result.Body).ToList();
                    return secretsBrokerAccounts;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get the DevOps Secrets Broker accounts from Safeguard. ");
            }

            return new List<DevOpsSecretsBrokerAccount>();
        }

        public object GetAvailableAccounts(ISafeguardConnection sgConnection, string filter, int? page, bool? count, int? limit, string orderby, string q)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                var p = new Dictionary<string, string>();
                JsonHelper.AddQueryParameter(p, nameof(filter), filter);
                JsonHelper.AddQueryParameter(p, nameof(page), page?.ToString());
                JsonHelper.AddQueryParameter(p, nameof(limit), limit?.ToString());
                JsonHelper.AddQueryParameter(p, nameof(count), count?.ToString());
                JsonHelper.AddQueryParameter(p, nameof(orderby), orderby);
                JsonHelper.AddQueryParameter(p, nameof(q), q);

                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, "PolicyAccounts", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    if (count == null || count.Value == false)
                    {
                        var accounts = JsonHelper.DeserializeObject<IEnumerable<SppAccount>>(result.Body);
                        if (accounts != null)
                        {
                            return accounts;
                        }
                    }
                    else
                    {
                        return result.Body;
                    }
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Get available accounts failed. {ex.Message}", ex);
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return new List<SppAccount>();
        }

        public AssetAccount GetAccount(ISafeguardConnection sgConnection, int id)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, $"AssetAccounts/{id}");
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var account = JsonHelper.DeserializeObject<AssetAccount>(result.Body);
                    if (account != null)
                    {
                        return account;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is SafeguardDotNetException &&
                    ((SafeguardDotNetException) ex).HttpStatusCode == HttpStatusCode.NotFound)
                {
                    _logger.Error($"Account not found for id '{id}': {ex.Message}");
                }
                else
                {
                    throw LogAndException($"Failed to get the account for id '{id}': {ex.Message}", ex);
                }
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return null;
        }

        public object GetAvailableA2ARegistrations(ISafeguardConnection sgConnection, string filter, int? page, bool? count, int? limit, string @orderby, string q)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                var f = "(not DevOpsInstanceId eq null)";
                f += filter != null ? " and " + filter : "";

                var p = new Dictionary<string, string>();
                JsonHelper.AddQueryParameter(p, nameof(filter), f);
                JsonHelper.AddQueryParameter(p, nameof(page), page?.ToString());
                JsonHelper.AddQueryParameter(p, nameof(limit), limit?.ToString());
                JsonHelper.AddQueryParameter(p, nameof(count), count?.ToString());
                JsonHelper.AddQueryParameter(p, nameof(orderby), orderby);
                JsonHelper.AddQueryParameter(p, nameof(q), q);

                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, "A2ARegistrations", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    if (count == null || count.Value == false)
                    {
                        var registrations = JsonHelper.DeserializeObject<IEnumerable<A2ARegistration>>(result.Body);
                        if (registrations != null)
                        {
                            return registrations;
                        }
                    }
                    else
                    {
                        return result.Body;
                    }
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Get available accounts failed. {ex.Message}", ex);
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return new List<A2ARegistration>();
        }

        public A2ARegistration SetA2ARegistration(ISafeguardConnection sgConnection, IMonitoringLogic monitoringLogic, IPluginsLogic pluginsLogic, int id)
        {
            //Turn off the monitor if it is running
            //Remove all of the mapped accounts to plugins
            //Remove all of the mapped vault accounts to plugins

            //Look up the new A2A registration to make sure that it exists in Safeguard
            //Update the configdb.svcId with the DevOpsInstanceId from the A2A registration;

            var sg = sgConnection ?? Connect();

            try
            {
                var a2aRegistration = GetA2ARegistration(sg, id);

                if (a2aRegistration != null && !string.IsNullOrEmpty(a2aRegistration.DevOpsInstanceId))
                {
                    monitoringLogic.EnableMonitoring(false);

                    _configDb.SvcId = a2aRegistration.DevOpsInstanceId;
                    _configDb.A2aUserId = a2aRegistration.CertificateUserId;

                    var a2aUser = GetA2AUser(sg, _configDb.A2aUserId);
                    _configDb.A2aUserId = a2aUser.Id;

                    //Just making these calls with the config ids set to null will realign everything.
                    _configDb.A2aRegistrationId = null;
                    _configDb.A2aVaultRegistrationId = null;
                    a2aRegistration = GetA2ARegistration(sg, A2ARegistrationType.Account);
                    GetA2ARegistration(sg, A2ARegistrationType.Vault);

                    var devOpsSecretsBroker = GetSecretsBrokerInstance(sg);
                    if (devOpsSecretsBroker != null)
                    {
                        devOpsSecretsBroker.DevOpsInstanceId = a2aRegistration.DevOpsInstanceId;
                        devOpsSecretsBroker.A2ARegistration = new A2ARegistration()
                        {
                            Id = a2aRegistration.Id
                        };
                        devOpsSecretsBroker.A2AUser = a2aUser;

                        var devOpsInstanceBody = JsonHelper.SerializeObject(devOpsSecretsBroker);

                        try
                        {
                            var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Put,
                                $"DevOps/SecretsBrokers/{devOpsSecretsBroker.Id}", devOpsInstanceBody);
                            if (result.StatusCode == HttpStatusCode.OK)
                            {
                                DevOpsSecretsBroker = JsonHelper.DeserializeObject<DevOpsSecretsBroker>(result.Body);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw LogAndException(
                                $"Failed to update the A2A registration associated with the Safeguard Secrets Broker instance {_configDb.SvcId}: {ex.Message}",
                                ex);
                        }
                    }

                    pluginsLogic.DeleteAccountMappings();
                    pluginsLogic.ClearMappedPluginVaultAccounts();

                    return a2aRegistration;
                }
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return null;
        }

        public A2ARetrievableAccount GetA2ARetrievableAccount(ISafeguardConnection sgConnection, int id, A2ARegistrationType registrationType)
        {
            if ((registrationType == A2ARegistrationType.Account && _configDb.A2aRegistrationId == null) ||
                (registrationType == A2ARegistrationType.Vault && _configDb.A2aVaultRegistrationId == null))
            {
                throw LogAndException("A2A registration not configured");
            }

            var registrationId = (registrationType == A2ARegistrationType.Account)
                ? _configDb.A2aRegistrationId
                : _configDb.A2aVaultRegistrationId;

            var sg = sgConnection ?? Connect();

            try
            {
                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get,
                    $"A2ARegistrations/{registrationId}/RetrievableAccounts/{id}");
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    return JsonHelper.DeserializeObject<A2ARetrievableAccount>(result.Body);
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Get retrievable account failed for account {id}", ex);
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return null;
        }

        public void DeleteA2ARetrievableAccount(ISafeguardConnection sgConnection, int id, A2ARegistrationType registrationType)
        {
            if ((registrationType == A2ARegistrationType.Account && _configDb.A2aRegistrationId == null) ||
                (registrationType == A2ARegistrationType.Vault && _configDb.A2aVaultRegistrationId == null))
            {
                throw LogAndException("A2A registration not configured");
            }

            var registrationId = (registrationType == A2ARegistrationType.Account)
                ? _configDb.A2aRegistrationId
                : _configDb.A2aVaultRegistrationId;

            var sg = sgConnection ?? Connect();

            try
            {
                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Delete,
                    $"A2ARegistrations/{registrationId}/RetrievableAccounts/{id}");
                if (result.StatusCode != HttpStatusCode.NoContent)
                {
                    _logger.Error($"(Failed to remove A2A retrievable account {id}");
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to remove A2A retrievable account {id}", ex);
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }
        }

        public IEnumerable<A2ARetrievableAccount> GetA2ARetrievableAccounts(ISafeguardConnection sgConnection, A2ARegistrationType registrationType)
        {
            if ((registrationType == A2ARegistrationType.Account && _configDb.A2aRegistrationId == null) ||
                (registrationType == A2ARegistrationType.Vault && _configDb.A2aVaultRegistrationId == null))
            {
                throw LogAndException("A2A registration not configured");
            }

            var registrationId = (registrationType == A2ARegistrationType.Account)
                ? _configDb.A2aRegistrationId
                : _configDb.A2aVaultRegistrationId;

            var sg = sgConnection ?? Connect();

            try
            {
                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get,
                    $"A2ARegistrations/{registrationId}/RetrievableAccounts");
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    return JsonHelper.DeserializeObject<IEnumerable<A2ARetrievableAccount>>(result.Body);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Get retrievable accounts failed: {ex.Message}");
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return new List<A2ARetrievableAccount>();
        }

        public A2ARetrievableAccount GetA2ARetrievableAccountById(ISafeguardConnection sgConnection, A2ARegistrationType registrationType, int accountId)
        {
            if ((registrationType == A2ARegistrationType.Account && _configDb.A2aRegistrationId == null) ||
                (registrationType == A2ARegistrationType.Vault && _configDb.A2aVaultRegistrationId == null))
            {
                throw LogAndException("A2A registration not configured");
            }

            var registrationId = (registrationType == A2ARegistrationType.Account)
                ? _configDb.A2aRegistrationId
                : _configDb.A2aVaultRegistrationId;

            var sg = sgConnection ?? Connect();

            try
            {
                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get,
                    $"A2ARegistrations/{registrationId}/RetrievableAccounts/{accountId}");
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    return JsonHelper.DeserializeObject<A2ARetrievableAccount>(result.Body);
                }
            }
            catch (SafeguardDotNetException ex)
            {
                if (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    _logger.Error($"Retrievable account {accountId} not found: {ex.Message}");
                    return null;
                }

                throw LogAndException($"Get retrievable account failed for account id {accountId}", ex);
            }
            catch (Exception ex)
            {
                throw LogAndException($"Get retrievable account failed for account id {accountId}", ex);
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return null;
        }

        public IEnumerable<A2ARetrievableAccount> AddA2ARetrievableAccounts(ISafeguardConnection sgConnection, IEnumerable<SppAccount> accounts, A2ARegistrationType registrationType)
        {
            if (_configDb.A2aRegistrationId == null)
            {
                throw LogAndException("A2A registration not configured");
            }

            var ipRestrictions = LocalIPAddress();
            var registrationId = (registrationType == A2ARegistrationType.Account)
                ? _configDb.A2aRegistrationId
                : _configDb.A2aVaultRegistrationId;

            var sg = sgConnection ?? Connect();

            try
            {
                foreach (var account in accounts)
                {
                    try
                    {
                        DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Post,
                            $"A2ARegistrations/{registrationId}/RetrievableAccounts",
                            $"{{\"AccountId\":{account.Id}, \"IpRestrictions\":[{ipRestrictions}]}}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to add account {account.Id} - {account.Name}: {ex.Message}");
                    }
                }

                return GetA2ARetrievableAccounts(sg, registrationType);
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }
        }

        public void RemoveA2ARetrievableAccounts(ISafeguardConnection sgConnection, IEnumerable<A2ARetrievableAccount> accounts, A2ARegistrationType registrationType)
        {
            if (_configDb.A2aRegistrationId == null)
            {
                throw LogAndException("A2A registration not configured");
            }

            var retrievableAccounts = accounts.ToArray();
            if (retrievableAccounts.All(x => x.AccountId == 0))
            {
                var msg = "Invalid list of accounts. Expecting a list of retrievable accounts.";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }

            var registrationId = (registrationType == A2ARegistrationType.Account)
                ? _configDb.A2aRegistrationId
                : _configDb.A2aVaultRegistrationId;

            var sg = sgConnection ?? Connect();

            try
            {
                foreach (var account in retrievableAccounts)
                {
                    try
                    {
                        DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Delete,
                            $"A2ARegistrations/{registrationId}/RetrievableAccounts/{account.AccountId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, 
                            $"Failed to remove account {account.AccountId} - {account.AccountName}: {ex.Message}");
                    }
                }
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }
        }

        public ServiceConfiguration GetDevOpsConfiguration(ISafeguardConnection sgConnection)
        {
            _serviceConfiguration.Clear();

            _serviceConfiguration.A2AUser = GetA2AUser(sgConnection);

            _serviceConfiguration.Appliance = GetSafeguardAvailability(sgConnection,
                new SafeguardDevOpsConnection()
                {
                    ApplianceAddress = _configDb.SafeguardAddress,
                    IgnoreSsl = _configDb.IgnoreSsl != null && _configDb.IgnoreSsl.Value,
                    ApiVersion = _configDb.ApiVersion ?? WellKnownData.DefaultApiVersion
                });

            _serviceConfiguration.A2ARegistration = GetA2ARegistration(sgConnection, A2ARegistrationType.Account);
            _serviceConfiguration.A2AVaultRegistration = GetA2ARegistration(sgConnection, A2ARegistrationType.Vault);
            _serviceConfiguration.Asset = GetAsset(sgConnection);
            _serviceConfiguration.AssetPartition = GetAssetPartition(sgConnection);

            return _serviceConfiguration;
        }

        private static volatile object _secretsBrokerInstanceLock = new object();
        public void AddSecretsBrokerInstance(ISafeguardConnection sgConnection)
        {
            lock (_secretsBrokerInstanceLock)
            {
                var sg = sgConnection ?? Connect();

                try
                {
                    var secretsBroker = GetSecretsBrokerInstanceByName(sg);
                    if (secretsBroker != null)
                    {
                        DevOpsSecretsBroker = secretsBroker;
                        return;
                    }

                    var addresses = GetLocalIPAddresses();
                    if (addresses != null && addresses.Any())
                    {
                        var ipAddress = addresses.FirstOrDefault();

                        secretsBroker = new DevOpsSecretsBroker()
                        {
                            Host = ipAddress.ToString(),
                            DevOpsInstanceId = _configDb.SvcId,
                            Asset = new Asset() {Name = WellKnownData.DevOpsAssetName(_configDb.SvcId)},
                            AssetPartition = new AssetPartition() {Name = WellKnownData.DevOpsAssetPartitionName(_configDb.SvcId)}
                        };

                        var secretsBrokerStr = JsonHelper.SerializeObject(secretsBroker);
                        try
                        {
                            var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Post, "DevOps/SecretsBrokers",
                                secretsBrokerStr);
                            if (result.StatusCode == HttpStatusCode.Created)
                            {
                                DevOpsSecretsBroker = JsonHelper.DeserializeObject<DevOpsSecretsBroker>(result.Body);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex,
                                $"Failed to create the DevOps Secrets Broker instance in Safeguard: {ex.Message}");
                        }
                    }
                    else
                    {
                        _logger.Error(
                            "Failed to create the DevOps Secrets Broker instance in Safeguard.  Unable to get the local IP address");
                    }
                }
                finally
                {
                    if (sgConnection == null)
                        sg.Dispose();
                }
            }
        }

        public void UpdateSecretsBrokerInstance(ISafeguardConnection sg, DevOpsSecretsBroker devOpsSecretsBroker)
        {
            if (devOpsSecretsBroker == null)
                throw LogAndException("Unable to update the devOps secrets broker instance.  The devOpsSecretsBroker cannot be null.");

            if (devOpsSecretsBroker.Host == null)
                throw LogAndException("Invalid devOps secrets broker instance.  The host cannot be null.");

            if (devOpsSecretsBroker.Asset == null)
                devOpsSecretsBroker.Asset = new Asset();

            if (devOpsSecretsBroker.Asset.Id == 0)
                devOpsSecretsBroker.Asset.Name = WellKnownData.DevOpsAssetName(_configDb.SvcId);

            if (devOpsSecretsBroker.AssetPartition == null)
                devOpsSecretsBroker.AssetPartition = new AssetPartition();

            if (devOpsSecretsBroker.AssetPartition.Id == 0)
                devOpsSecretsBroker.AssetPartition.Name = WellKnownData.DevOpsAssetPartitionName(_configDb.SvcId);

            var devopsSecretsBrokerStr = JsonHelper.SerializeObject(devOpsSecretsBroker);
            try
            {
                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Put, 
                    $"DevOps/SecretsBrokers/{devOpsSecretsBroker.Id}", devopsSecretsBrokerStr);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    DevOpsSecretsBroker = JsonHelper.DeserializeObject<DevOpsSecretsBroker>(result.Body);
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to update the devops instance.", ex);
            }
        }

        public Asset GetAsset(ISafeguardConnection sgConnection)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                FullResponse result;

                // If we don't have an asset Id then try to find the asset by name
                if (_configDb.AssetId == null)
                {
                    var ipAddress = LocalIPAddress();
                    var knownAssetName = WellKnownData.DevOpsAssetName(_configDb.SvcId);
                    try
                    {
                        var p = new Dictionary<string, string>
                            {{"filter", $"Name eq '{knownAssetName}'"}};

                        result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, "Assets", null, p);
                        if (result.StatusCode == HttpStatusCode.OK)
                        {
                            var foundAssets = JsonHelper.DeserializeObject<List<Asset>>(result.Body);

                            if (foundAssets.Count > 0)
                            {
                                var asset = foundAssets.FirstOrDefault();
                                if (asset != null)
                                {
                                    _configDb.AssetId = asset.Id;
                                }

                                return asset;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to get the asset by name {knownAssetName}: {ex.Message}");
                    }
                }
                else // Otherwise just get the asset by id
                {
                    var assetId = _configDb.AssetId;
                    try
                    {
                        var asset = GetAsset(sg, DevOpsSecretsBroker.Id);
                        if (asset != null)
                        {
                            return asset;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Just log the error and move on.  This will set the asset id in the database to null.
                        //  If there is actually an asset that matches, it will find it the next time.
                        _logger.Error(ex, $"Failed to get the asset for id '{assetId}': {ex.Message}");
                    }
                }

                // Apparently the asset id we have is wrong so get rid of it.
                _configDb.AssetId = null;
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return null;
        }

        private Asset GetAsset(ISafeguardConnection sg, int? id)
        {
            if (id != null)
            {
                try
                {
                    var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, $"DevOps/SecretsBrokers/{id}/Asset");
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        return JsonHelper.DeserializeObject<Asset>(result.Body);
                    }
                }
                catch (SafeguardDotNetException ex)
                {
                    if (ex.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.Error(ex, $"Asset not found for id '{id}'");
                    }
                    else
                    {
                        throw LogAndException($"Failed to get the asset for id '{id}'", ex);
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndException($"Failed to get the asset for id '{id}'", ex);
                }
            }

            return null;
        }

        public AssetPartition GetAssetPartition(ISafeguardConnection sgConnection)
        {
            var sg = sgConnection ?? Connect();

            try
            {
                FullResponse result;

                // If we don't have an asset partition Id then try to find the asset partition by name
                if (_configDb.AssetPartitionId == null)
                {
                    var knownAssetPartitionName = WellKnownData.DevOpsAssetPartitionName(_configDb.SvcId);
                    try
                    {
                        var p = new Dictionary<string, string>
                            {{"filter", $"Name eq '{knownAssetPartitionName}'"}};

                        result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, "AssetPartitions", null, p);
                        if (result.StatusCode == HttpStatusCode.OK)
                        {
                            var foundAssetPartitions = JsonHelper.DeserializeObject<List<AssetPartition>>(result.Body);

                            if (foundAssetPartitions.Count > 0)
                            {
                                var assetPartition = foundAssetPartitions.FirstOrDefault();
                                if (assetPartition != null)
                                {
                                    _configDb.AssetPartitionId = assetPartition.Id;
                                }

                                return assetPartition;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to get the asset partition by name {knownAssetPartitionName}: {ex.Message}");
                    }
                }
                else // Otherwise just get the registration by id
                {
                    var assetPartitionId = _configDb.AssetPartitionId;
                    try
                    {
                        var assetPartition = GetAssetPartition(sg, assetPartitionId);
                        if (assetPartition != null)
                        {
                            return assetPartition;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Just log the error and move on. If there is an asset partition that matches, Secrets Broker will find it
                        //  the next time.
                        _logger.Error(ex, $"Failed to get the asset partition for id '{assetPartitionId}': {ex.Message}");
                    }
                }

                // Apparently the asset partition id we have is wrong so get rid of it.
                _configDb.AssetPartitionId = null;
            }
            finally
            {
                if (sgConnection == null)
                    sg.Dispose();
            }

            return null;
        }

        private AssetPartition GetAssetPartition(ISafeguardConnection sg, int? id)
        {
            if (id != null)
            {
                try
                {
                    var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, $"AssetPartitions/{id}");
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        return JsonHelper.DeserializeObject<AssetPartition>(result.Body);
                    }
                }
                catch (SafeguardDotNetException ex)
                {
                    if (ex.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.Error($"Asset partition not found for id '{id}'", ex);
                    }
                    else
                    {
                        throw LogAndException($"Failed to get the asset partition for id '{id}'", ex);
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndException($"Failed to get the asset partition for id '{id}'", ex);
                }
            }

            return null;
        }

        public void Dispose()
        {
            DisconnectWithAccessToken();
        }

//TODO: Delete me when done testing the authorization scheme.
        public void TestCertConnection()
        {
            var sg = CertConnect();

            try
            {
                if (sg == null)
                    return;

                var h = new Dictionary<string, string>
                    {{"devOpsInstanceId", _configDb.SvcId}};

                var result = DevOpsInvokeMethodFull(_configDb.SvcId, sg, Service.Core, Method.Get, "DevOps/SecretsBrokers/TestEndPoint", null, null, h);
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to call the test endpoint: {ex.Message}");
            }
            finally
            {
                sg?.Dispose();
            }
        }
    }
}
