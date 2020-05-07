using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using Microsoft.AspNetCore.WebUtilities;
using OneIdentity.DevOps.Exceptions;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;

namespace OneIdentity.DevOps.Logic
{
    internal class SafeguardLogic : ISafeguardLogic, IDisposable
    {
        private const int DefaultApiVersion = 3;

        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;

        private ServiceConfiguration _serviceConfiguration;

        public SafeguardLogic(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
        }

        private DevOpsException LogAndThrow(string msg, Exception ex = null)
        {
            _logger.Error(msg);
            return new DevOpsException(msg, ex);
        }

        private SafeguardConnection GetSafeguardAppliance(ISafeguardConnection sg)
        {
            try
            {
                var availabilityJson = sg.InvokeMethod(Service.Notification, Method.Get, "Status/Availability");
                var applianceAvailability = JsonHelper.DeserializeObject<ApplianceAvailability>(availabilityJson);
                return new SafeguardConnection()
                {
                    ApplianceAddress = _configDb.SafeguardAddress,
                    ApplianceId = applianceAvailability.ApplianceId,
                    ApplianceName = applianceAvailability.ApplianceName,
                    ApplianceVersion = applianceAvailability.ApplianceVersion,
                    ApplianceState = applianceAvailability.ApplianceCurrentState
                };
            }
            catch (SafeguardDotNetException ex)
            {
                throw new DevOpsException($"Failed to get the appliance information: {ex.Message}");
            }
        }

        private SafeguardConnection GetSafeguardAvailability(ISafeguardConnection sg, ref SafeguardConnection availability)
        {
            var safeguard = GetSafeguardAppliance(sg);
            availability.ApplianceId = safeguard.ApplianceId;
            availability.ApplianceName = safeguard.ApplianceName;
            availability.ApplianceVersion = safeguard.ApplianceVersion;
            availability.ApplianceState = safeguard.ApplianceState;
            return availability;
        }

        private SafeguardConnection FetchAndStoreSignatureCertificate(SafeguardConnection availability)
        {
            HttpClientHandler handler = null;
            if (availability.IgnoreSsl)
            {
                handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) =>
                    {
                        return true;
                    };
            }

            string result = null;
            using (var client = new HttpClient(handler))
            {
                client.BaseAddress = new Uri($"https://{availability.ApplianceAddress}");
                var response = client.GetAsync("RSTS/Saml2FedMetadata").Result;
                response.EnsureSuccessStatusCode();

                result = response.Content.ReadAsStringAsync().Result;
            }

            if (result != null)
            {
                var xml = new XmlDocument();
                xml.LoadXml(result);
                var certificates = xml.DocumentElement.GetElementsByTagName("X509Certificate");
                if (certificates != null && certificates.Count > 0)
                {
                    _configDb.SigningCertificate = certificates.Item(0).InnerText;
                }
            }

            return availability;
        }

        private SafeguardConnection ConnectAnonymous(string safeguardAddress, int apiVersion, bool ignoreSsl)
        {
            ISafeguardConnection sg = null;
            try
            {
                var availability = new SafeguardConnection
                {
                    ApplianceAddress = safeguardAddress,
                    IgnoreSsl = ignoreSsl
                };
                sg = SafeguardDotNet.Safeguard.Connect(safeguardAddress, apiVersion, ignoreSsl);
                return GetSafeguardAvailability(sg, ref availability);
            }
            catch (SafeguardDotNetException ex)
            {
                throw LogAndThrow($"Failed to contact Safeguard at '{safeguardAddress}': {ex.Message}", ex);
            }
            finally
            {
                sg?.Dispose();
            }
        }

        private ISafeguardConnection ConnectWithAccessToken(string token)
        {
            if (_serviceConfiguration != null)
            {
                DisconnectWithAccessToken();
            }

            if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                throw new DevOpsException("Missing safeguard appliance configuration.");
            if (string.IsNullOrEmpty(token))
                throw new DevOpsException("Missing safeguard access token.");

            _serviceConfiguration = new ServiceConfiguration
            {
                AccessToken = token.ToSecureString()
            };

            return Connect();
        }

        private void DisconnectWithAccessToken()
        {
            _serviceConfiguration?.AccessToken?.Dispose();
            _serviceConfiguration = null;
        }

        private bool GetAndValidateUserPermissions(string token)
        {
            ISafeguardConnection sg = null;
            try
            {
                sg = ConnectWithAccessToken(token);

                var meJson = sg.InvokeMethod(Service.Core, Method.Get, "Me");
                var loggedInUser = JsonHelper.DeserializeObject<LoggedInUser>(meJson);

                var valid = loggedInUser.AdminRoles.Any(x => x.Equals("ApplianceAdmin") || x.Equals("OperationsAdmin"));
                if (valid)
                {
                    AuthorizedCache.Instance.Add(new ServiceConfiguration(loggedInUser)
                    {
                        AccessToken = token.ToSecureString(),
                        Appliance = GetSafeguardAppliance(sg)
                    });
                }

                return valid;
            }
            finally
            {
                sg?.Dispose();
            }
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
                    PrimaryAuthenticationIdentity = thumbprint
                };

                var a2aUserStr = JsonHelper.SerializeObject(a2aUser);
                try
                {
                    var result = sg.InvokeMethodFull(Service.Core, Method.Post, "Users", a2aUserStr);
                    if (result.StatusCode == HttpStatusCode.Created)
                    {
                        a2aUser = JsonHelper.DeserializeObject<A2AUser>(result.Body);
                        _configDb.A2aUserId = a2aUser.Id;
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndThrow($"Failed to create the A2A user: {ex.Message}", ex);
                }
            }
            else
            {
                if (!a2aUser.PrimaryAuthenticationIdentity.Equals(thumbprint, StringComparison.InvariantCultureIgnoreCase))
                {
                    try { 
                        a2aUser.PrimaryAuthenticationIdentity = thumbprint;
                        var a2aUserStr = JsonHelper.SerializeObject(a2aUser);
                        sg.InvokeMethodFull(Service.Core, Method.Put, $"Users/{a2aUser.Id}", a2aUserStr);
                    }
                    catch (Exception ex)
                    {
                        throw LogAndThrow($"Failed to update the A2A user: {ex.Message}", ex);
                    }
                }
            }
        }

        private A2AUser GetA2AUser(ISafeguardConnection sg)
        {
            FullResponse result;

            // If we don't have a user Id then try to find the user by name
            if (_configDb.A2aUserId == null)
            {
                var p = new Dictionary<string, string> {{"filter", $"UserName eq '{WellKnownData.DevOpsUserName}'"}};

                try
                {
                    result = sg.InvokeMethodFull(Service.Core, Method.Get, "Users", null, p);
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        var foundUsers = JsonHelper.DeserializeObject<List<A2AUser>>(result.Body);

                        if (foundUsers.Count > 0)
                        {
                            var a2aUser = foundUsers.FirstOrDefault();
                            _configDb.A2aUserId = a2aUser.Id;
                            return a2aUser;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to get the A2A user by name: {ex.Message}");
                }
            }
            else // Otherwise just get the user by id
            {
                try
                {
                    result = sg.InvokeMethodFull(Service.Core, Method.Get, $"Users/{_configDb.A2aUserId}");
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        return JsonHelper.DeserializeObject<A2AUser>(result.Body);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to get the A2A user by id {_configDb.A2aUserId}: {ex.Message}");
                }

                // Apparently the user id we have is wrong so get rid of it.
                _configDb.A2aUserId = null;
            }

            return null;
        }

        private void AddTrustedCertificate(ISafeguardConnection sg)
        {
            var thumbprint = _configDb.UserCertificate?.Thumbprint;

            if (thumbprint != null)
            {
                FullResponse result = null;
                try
                {
                    result = sg.InvokeMethodFull(Service.Core, Method.Get, $"TrustedCertificates/{thumbprint}");
                }
                catch (Exception ex)
                {
                    if (ex is SafeguardDotNetException && ((SafeguardDotNetException)ex).HttpStatusCode != HttpStatusCode.NotFound)
                    {
                        throw LogAndThrow($"Failed to add the trusted certificate '{_configDb.SafeguardAddress}': {ex.Message}", ex);
                    }
                }

                if (result == null || result.StatusCode == HttpStatusCode.NotFound)
                {
                    var certData = _configDb.UserCertificate.Export(X509ContentType.Cert);
                    var trustedCert = new TrustedCertificate()
                    {
                        Base64CertificateData = Convert.ToBase64String(certData)
                    };

                    var trustedCertStr = JsonHelper.SerializeObject(trustedCert);
                    try
                    {
                        sg.InvokeMethodFull(Service.Core, Method.Post, "TrustedCertificates", trustedCertStr);
                    }
                    catch (Exception ex)
                    {
                        throw LogAndThrow($"Failed to add the trusted certificate '{_configDb.SafeguardAddress}': {ex.Message}", ex);
                    }
                }
            }
        }

        private void CreateA2ARegistration(ISafeguardConnection sg)
        {
            if (_configDb.A2aUserId == null)
                throw new DevOpsException("Failed to create A2A registration due to missing A2A user");

            var a2aRegistration = GetA2ARegistration(sg);
            if (a2aRegistration == null)
            {
                var registration = new A2ARegistration()
                {
                    AppName = WellKnownData.DevOpsServiceName,
                    CertificateUserId = _configDb.A2aUserId.Value,
                    VisibleToCertificateUsers = true
                };

                var registrationStr = JsonHelper.SerializeObject(registration);

                try
                {
                    var result = sg.InvokeMethodFull(Service.Core, Method.Post, "A2ARegistrations", registrationStr);
                    if (result.StatusCode == HttpStatusCode.Created)
                    {
                        registration = JsonHelper.DeserializeObject<A2ARegistration>(result.Body);
                        _configDb.A2aRegistrationId = registration.Id;
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndThrow($"Failed to create the A2A registration: {ex.Message}", ex);
                }
            }
        }

        private A2ARegistration GetA2ARegistration(ISafeguardConnection sg)
        {
            FullResponse result;

            // If we don't have a registration Id then try to find the registration by name
            if (_configDb.A2aRegistrationId == null)
            {
                try
                {
                    var p = new Dictionary<string, string>
                        {{"filter", $"AppName eq '{WellKnownData.DevOpsServiceName}'"}};

                    result = sg.InvokeMethodFull(Service.Core, Method.Get, "A2ARegistrations", null, p);
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        var foundRegistrations = JsonHelper.DeserializeObject<List<A2ARegistration>>(result.Body);

                        if (foundRegistrations.Count > 0)
                        {
                            var registration = foundRegistrations.FirstOrDefault();
                            _configDb.A2aRegistrationId = registration.Id;
                            return registration;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to get the A2A user by name {WellKnownData.DevOpsServiceName}: {ex.Message}");
                }
            }
            else // Otherwise just get the registration by id
            {
                try
                {
                    result = sg.InvokeMethodFull(Service.Core, Method.Get,
                        $"A2ARegistrations/{_configDb.A2aRegistrationId}");
                    if (result.StatusCode == HttpStatusCode.OK)
                    {
                        return JsonHelper.DeserializeObject<A2ARegistration>(result.Body);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is SafeguardDotNetException && ((SafeguardDotNetException)ex).HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.Error($"Registration not found for id '{_configDb.A2aRegistrationId}': {ex.Message}");
                    }
                    else
                    {
                        throw LogAndThrow($"Failed to get the registration for id '{_configDb.A2aRegistrationId}': {ex.Message}", ex);
                    }
                }
            }

            // Apparently the registration id we have is wrong so get rid of it.
            _configDb.A2aRegistrationId = null;

            return null;
        }

        public void RemoveClientCertificate()
        {
            _configDb.UserCertificateBase64Data = null;
            _configDb.UserCertificatePassphrase = null;
            _configDb.UserCertificateThumbprint = null;
        }


        public ISafeguardConnection Connect()
        {
            if (_serviceConfiguration == null)
            {
                throw new DevOpsException("Not logged in");
            }

            try
            {
                return SafeguardDotNet.Safeguard.Connect(_configDb.SafeguardAddress, _serviceConfiguration.AccessToken,
                    _configDb.ApiVersion ?? DefaultApiVersion, _configDb.IgnoreSsl ?? false);

            }
            catch (SafeguardDotNetException ex)
            {
                throw LogAndThrow($"Failed to connect to Safeguard at '{_configDb.SafeguardAddress}': {ex.Message}", ex);
            }
        }

        public bool ValidateLogin(string token, bool tokenOnly = false)
        {
            try
            {
                var key = _configDb.SigningCertificate;
                var bytes = Convert.FromBase64String(key);
                var cert = new X509Certificate2(bytes);

                var parts = token.Split('.');
                var header = parts[0];
                var payload = parts[1];
                var signature = Base64UrlTextEncoder.Decode(parts[2]);

                var data = Encoding.UTF8.GetBytes(header + '.' + payload);

                bool validToken = cert.GetRSAPublicKey()
                    .VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                if (validToken && !tokenOnly)
                {
                    return GetAndValidateUserPermissions(token);
                }

                return validToken;
            } catch { }

            return false;
        }

        public ClientCertificate GetClientCertificate()
        {
            var cert = _configDb.UserCertificate;

            if (cert != null)
            {
                var result = new ClientCertificate()
                {
                    Thumbprint = cert.Thumbprint,
                    IssuedBy = cert.Issuer,
                    Subject = cert.Subject,
                    NotAfter = cert.NotBefore,
                    NotBefore = cert.NotAfter
                };
                return result;
            }

            return null;
        }

        public void InstallClientCertificate(ClientCertificate certificate)
        {
            var certData = Regex.Replace(certificate.Base64CertificateData, "-----BEGIN .*-----", "");
            certData = Regex.Replace(certData, "-----END .*", "");
            certData = certData.Replace("\r\n", "").Replace("\n", "");

            X509Certificate2 cert;
            try
            {
                var certificateBytes = Convert.FromBase64String(certData);
                cert = certificate.Passphrase == null
                    ? new X509Certificate2(certificateBytes)
                    : new X509Certificate2(certificateBytes, certificate.Passphrase);
            }
            catch (Exception ex)
            {
                throw LogAndThrow($"Failed to convert the provided certificate: {ex.Message}", ex);
            }

            if (cert.HasPrivateKey)
            {
                _configDb.UserCertificatePassphrase = certificate.Passphrase;
                _configDb.UserCertificateBase64Data = certificate.Base64CertificateData;
            }
            else
            {
                try
                {
                    using var rsa = RSA.Create();
                    var privateKeyBytes = Convert.FromBase64String(_configDb.CsrPrivateKeyBase64Data);
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

                    using (X509Certificate2 pubOnly = cert)
                    using (X509Certificate2 pubPrivEphemeral = pubOnly.CopyWithPrivateKey(rsa))
                    {
                        _configDb.UserCertificatePassphrase = null;
                        _configDb.UserCertificateBase64Data = Convert.ToBase64String(pubPrivEphemeral.Export(X509ContentType.Pfx));
                    }
                }
                catch (Exception ex)
                {
                    throw LogAndThrow($"Failed to import the certificate: {ex.Message}", ex);
                }
            }
        }

        public string GetClientCSR(int? size, string subjectName)
        {
            int certSize = 2048;
            string certSubjectName = "CN=DevOpsServiceClientCertificate";

            if (size != null)
                certSize = size.Value;
            if (subjectName != null)
            {
                if (!subjectName.StartsWith("CN=", StringComparison.InvariantCultureIgnoreCase))
                    subjectName = "CN={subjectName}";
                certSubjectName = subjectName;
            }

            using (RSA rsa = RSA.Create(certSize))
            {
                var certificateRequest = new CertificateRequest(certSubjectName, rsa,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                certificateRequest.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false));

                certificateRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement,
                        true));

                certificateRequest.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection
                        {
                            new Oid("1.3.6.1.5.5.7.3.2")
                        },
                        true));

                certificateRequest.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false));

                var csr = certificateRequest.CreateSigningRequest();
                _configDb.CsrBase64Data = Convert.ToBase64String(csr);
                _configDb.CsrPrivateKeyBase64Data = Convert.ToBase64String(rsa.ExportRSAPrivateKey());

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("-----BEGIN CERTIFICATE REQUEST-----");
                builder.AppendLine(Convert.ToBase64String(csr, Base64FormattingOptions.InsertLineBreaks));
                builder.AppendLine("-----END CERTIFICATE REQUEST-----");

                return builder.ToString();
            }
        }

        public ServiceConfiguration ConfigureDevOpsService()
        {
            if (_serviceConfiguration == null)
                throw new DevOpsException("Not logged in");

            using var sg = Connect();
            CreateA2AUser(sg);
            AddTrustedCertificate(sg);
            CreateA2ARegistration(sg);

            return GetDevOpsConfiguration();
        }

        public SafeguardConnection GetSafeguardConnection()
        {
            if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                return null;
            return ConnectAnonymous(_configDb.SafeguardAddress, _configDb.ApiVersion ?? DefaultApiVersion, _configDb.IgnoreSsl ?? false);
        }

        public SafeguardConnection SetSafeguardData(SafeguardData safeguardData)
        {
            var availability = ConnectAnonymous(safeguardData.NetworkAddress,
                safeguardData.ApiVersion ?? DefaultApiVersion, safeguardData.IgnoreSsl ?? false);

            if (availability != null)
            {
                _configDb.SafeguardAddress = safeguardData.NetworkAddress;
                _configDb.ApiVersion = safeguardData.ApiVersion ?? DefaultApiVersion;
                _configDb.IgnoreSsl = safeguardData.IgnoreSsl ?? false;
            }

            FetchAndStoreSignatureCertificate(availability);

            return availability;
        }

        // public void DeleteDevOpsConfiguration()
        // {
        //     _configDb.SafeguardAddress = null;
        //     _configDb.ApiVersion = null;
        //     _configDb.IgnoreSsl = null;
        //     _configDb.A2aRegistrationId = null;
        //     _configDb.A2aUserId = null;
        //     _configDb.CsrPrivateKeyBase64Data = null;
        //     _configDb.CsrBase64Data = null;
        //     _configDb.UserCertificateBase64Data = null;
        //     _configDb.UserCertificatePassphrase = null;
        //     _configDb.UserCertificateThumbprint = null;
        //
        //     //TODO: Need to remove the A2AUser, A2ARegistration and ClientCertificate from the Safeguard appliance.
        // }

        public IEnumerable<SppAccount> GetAvailableAccounts()
        {
            using var sg = Connect();

            try
            {
                var p = new Dictionary<string, string> {{"fields", "Account"}};

                var result = sg.InvokeMethodFull(Service.Core, Method.Get, "Me/RequestEntitlements", null, p);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var accounts = JsonHelper.DeserializeObject<IEnumerable<SppAccountWrapper>>(result.Body);
                    if (accounts != null)
                    {
                        return accounts.Select(x => x.Account);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Get available accounts failed: {ex.Message}");
            }

            return new List<SppAccount>();
        }

        public A2ARegistration GetA2ARegistration()
        {
            using var sg = Connect();

            return GetA2ARegistration(sg);
        }

        public void DeleteA2ARegistration()
        {
            if (_configDb.A2aRegistrationId == null)
            {
                var msg = "A2A registration not configured";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }

            using var sg = Connect();

            A2ARegistration registration = null;
            try
            {
                registration = GetA2ARegistration(sg);
                if (registration != null)
                {
                    var result = sg.InvokeMethodFull(Service.Core, Method.Delete,
                        $"A2ARegistrations/{registration.Id}");
                    _configDb.DeleteAccountMappings();
                    _serviceConfiguration.A2ARegistrationName = null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to delete the registration {_configDb.A2aRegistrationId} - {registration?.AppName}: {ex.Message}");
            }

            A2AUser user = null;
            try
            {
                user = GetA2AUser(sg);
                if (user != null)
                {
                    var result = sg.InvokeMethodFull(Service.Core, Method.Delete, $"Users/{user.Id}");
                    _configDb.DeleteAccountMappings();
                    _serviceConfiguration.UserName = null;
                    _serviceConfiguration.IdentityProviderName = null;
                    _serviceConfiguration.AdminRoles = null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to delete the A2A certificate user {_configDb.A2aUserId} - {user?.UserName}: {ex.Message}");
            }

            try
            {
                var thumbprint = _configDb.UserCertificate?.Thumbprint;
                if (thumbprint != null)
                {
                    sg.InvokeMethodFull(Service.Core, Method.Delete, $"TrustedCertificates/{thumbprint}");
                    RemoveClientCertificate();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to remove the A2A trusted certificate {_configDb.UserCertificate?.Thumbprint} - {user?.UserName}: {ex.Message}");
            }

        }

        public IEnumerable<A2ARetrievableAccount> GetA2ARetrievableAccounts()
        {
            if (_configDb.A2aRegistrationId == null)
            {
                throw LogAndThrow("A2A registration not configured");
            }

            using var sg = Connect();

            try
            {
                var result = sg.InvokeMethodFull(Service.Core, Method.Get, $"A2ARegistrations/{_configDb.A2aRegistrationId}/RetrievableAccounts");
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    return JsonHelper.DeserializeObject<IEnumerable<A2ARetrievableAccount>>(result.Body);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Get retrievable accounts failed: {ex.Message}");
            }

            return new List<A2ARetrievableAccount>();
        }

        public IEnumerable<A2ARetrievableAccount> AddA2ARetrievableAccounts(IEnumerable<SppAccount> accounts)
        {
            if (_configDb.A2aRegistrationId == null)
            {
                throw LogAndThrow("A2A registration not configured");
            }

            using var sg = Connect();

            foreach (var account in accounts)
            {
                try
                {
                    sg.InvokeMethodFull(Service.Core, Method.Post, $"A2ARegistrations/{_configDb.A2aRegistrationId}/RetrievableAccounts",
                        $"{{\"AccountId\":{account.Id}}}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to add account {account.Id} - {account.Name}: {ex.Message}");
                }
            }

            return GetA2ARetrievableAccounts();
        }


        public ServiceConfiguration GetDevOpsConfiguration()
        {
            _serviceConfiguration.IdentityProviderName = null;
            _serviceConfiguration.UserName = null;
            _serviceConfiguration.AdminRoles = null;
            _serviceConfiguration.A2ARegistrationName = null;
            _serviceConfiguration.Thumbprint = null;

            using var sg = Connect();
            var a2aUser = GetA2AUser(sg);
            if (a2aUser != null)
            {
                _serviceConfiguration.IdentityProviderName = a2aUser.IdentityProviderName;
                _serviceConfiguration.UserName = a2aUser.UserName;
                _serviceConfiguration.AdminRoles = a2aUser.AdminRoles;
            }

            _serviceConfiguration.Appliance = GetSafeguardAppliance(sg);

            var a2aRegistration = GetA2ARegistration(sg);
            if (a2aRegistration != null)
            {
                _serviceConfiguration.A2ARegistrationName = a2aRegistration.AppName;
            }

            _serviceConfiguration.Thumbprint = _configDb.UserCertificate?.Thumbprint;

            return _serviceConfiguration;
        }

        public void Dispose()
        {
            DisconnectWithAccessToken();
        }
    }
}
