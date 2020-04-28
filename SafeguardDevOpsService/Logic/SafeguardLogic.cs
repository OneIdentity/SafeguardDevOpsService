using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.SafeguardDotNet;
using Safeguard = OneIdentity.DevOps.Data.Safeguard;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi.Models;
using OneIdentity.DevOps.Exceptions;

namespace OneIdentity.DevOps.Logic
{
    internal class SafeguardLogic : ISafeguardLogic, IDisposable
    {
        private const int DefaultApiVersion = 3;

        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;

        private ManagementConnection _connectionContext;

        public SafeguardLogic(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
        }

        private Safeguard GetSafeguardAppliance(ISafeguardConnection sg)
        {
            try
            {
                var availabilityJson = sg.InvokeMethod(Service.Notification, Method.Get, "Status/Availability");
                var applianceAvailability = JsonHelper.DeserializeObject<ApplianceAvailability>(availabilityJson);
                return new Safeguard()
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

        private Safeguard GetSafeguardAvailability(ISafeguardConnection sg, ref Safeguard availability)
        {
            var safeguard = GetSafeguardAppliance(sg);
            availability.ApplianceId = safeguard.ApplianceId;
            availability.ApplianceName = safeguard.ApplianceName;
            availability.ApplianceVersion = safeguard.ApplianceVersion;
            availability.ApplianceState = safeguard.ApplianceState;
            return availability;
        }

        private Safeguard FetchAndStoreSignatureCertificate(Safeguard availability)
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

        private Safeguard ConnectAnonymous(string safeguardAddress, int apiVersion, bool ignoreSsl)
        {
            ISafeguardConnection sg = null;
            try
            {
                var availability = new Safeguard
                {
                    ApplianceAddress = safeguardAddress,
                    IgnoreSsl = ignoreSsl
                };
                sg = SafeguardDotNet.Safeguard.Connect(safeguardAddress, apiVersion, ignoreSsl);
                return GetSafeguardAvailability(sg, ref availability);
            }
            catch (SafeguardDotNetException ex)
            {
                _logger.Error($"Failed to contact Safeguard at '{safeguardAddress}': {ex.Message}");
                return null; // TODO: return error?
            }
            finally
            {
                sg?.Dispose();
            }
        }

        private ISafeguardConnection ConnectWithAccessToken(string token)
        {
            if (_connectionContext != null)
            {
                DisconnectWithAccessToken();
            }

            try
            {
                if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                    throw new DevOpsException("Missing safeguard appliance configuration.");
                if (string.IsNullOrEmpty(token))
                    throw new DevOpsException("Missing safeguard access token.");

                _connectionContext = new ManagementConnection
                {
                    AccessToken = token.ToSecureString()
                };

                return SafeguardDotNet.Safeguard.Connect(_configDb.SafeguardAddress, _connectionContext.AccessToken,
                    _configDb.ApiVersion ?? DefaultApiVersion, _configDb.IgnoreSsl ?? false);
            }
            catch (SafeguardDotNetException ex)
            {
                var msg = $"Failed to connect to Safeguard at '{_configDb.SafeguardAddress}': {ex.Message}";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }
        }

        private void ConnectWithAccessToken(ManagementConnectionData connectionData)
        {
            if (_connectionContext != null)
            {
                DisconnectWithAccessToken();
            }

            ISafeguardConnection sg = null;
            try
            {
                if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                    return; // TODO: errors?
                if (string.IsNullOrEmpty(connectionData.AccessToken))
                    return; // TODO: errors?

                _connectionContext = new ManagementConnection
                {
                    AccessToken = connectionData.AccessToken.ToSecureString()
                };
                var availability = new Safeguard
                {
                    ApplianceAddress = _configDb.SafeguardAddress,
                    IgnoreSsl = connectionData.IgnoreSsl || (_configDb.IgnoreSsl ?? false)
                };
                sg = SafeguardDotNet.Safeguard.Connect(availability.ApplianceAddress, _connectionContext.AccessToken,
                    _configDb.ApiVersion ?? DefaultApiVersion, availability.IgnoreSsl);
                _connectionContext.Appliance = GetSafeguardAvailability(sg, ref availability);
                var meJson = sg.InvokeMethod(Service.Core, Method.Get, "Me");
                var loggedInUser = JsonHelper.DeserializeObject<LoggedInUser>(meJson);

                _connectionContext.IdentityProviderName = loggedInUser.IdentityProviderName;
                _connectionContext.UserName = loggedInUser.UserName;
                _connectionContext.AdminRoles = loggedInUser.AdminRoles;
            }
            catch (SafeguardDotNetException ex)
            {
                var msg = $"Failed to connect to Safeguard at '{_configDb.SafeguardAddress}': {ex.Message}";
                _logger.Error(msg);
                throw new DevOpsException(msg);
            }
            finally
            {
                sg?.Dispose();
            }
        }

        private string ExecuteCommand(Method method, string path, string body = null, Dictionary<string,string> parameters = null)
        {
            if (_connectionContext == null)
            {
                throw new DevOpsException("Not logged in");
            }

            ISafeguardConnection sg = null;
            try
            {
                sg = SafeguardDotNet.Safeguard.Connect(_configDb.SafeguardAddress, _connectionContext.AccessToken,
                    _configDb.ApiVersion ?? DefaultApiVersion, _configDb.IgnoreSsl ?? false);

                return sg.InvokeMethod(Service.Core, method, path, body, parameters);
            }
            catch (SafeguardDotNetException ex)
            {
                var msg = $"Failed to execute command '{path}': {ex.Message}";
                _logger.Error(msg);
                throw new DevOpsException(msg, ex);
            }
            finally
            {
                sg?.Dispose();
            }
        }

        private void DisconnectWithAccessToken()
        {
            _connectionContext?.AccessToken?.Dispose();
            _connectionContext = null;
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
                    AuthorizedCache.Instance.Add(new ManagementConnection(loggedInUser)
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

        private void CreateA2ACertificateUser()
        {
            string thumbprint = _configDb.UserCertificate?.Thumbprint;

            if (thumbprint != null)
            {
                var p = new Dictionary<string, string>();
                p.Add("filter", $"UserName eq '{A2AUser.DevOpsUserName}'");

                var result = ExecuteCommand(Method.Get, "Users", null, p);
                var foundUsers = JsonHelper.DeserializeObject<List<A2AUser>>(result);

                var a2aUser = foundUsers.Count > 0 ? foundUsers.FirstOrDefault() : new A2AUser();
                a2aUser.PrimaryAuthenticationIdentity = thumbprint;
                var a2aUserStr = JsonHelper.SerializeObject(a2aUser);
                var path = foundUsers.Count > 0 ? $"Users/{a2aUser.Id}" : "Users";

                ExecuteCommand(foundUsers.Count > 0 ? Method.Put : Method.Post, path, a2aUserStr);
            }
        }

        private void AddTrustedCertificate()
        {
            string thumbprint = _configDb.UserCertificate?.Thumbprint;

            if (thumbprint != null)
            {
                string result = null;
                try
                {
                    result = ExecuteCommand(Method.Get, $"TrustedCertificates/{thumbprint}");
                }
                catch (Exception ex)
                {
                    var z = ex;
                }

                if (result == null)
                {
                    var certData = _configDb.UserCertificate.Export(X509ContentType.Cert);
                    var trustedCert = new TrustedCertificate()
                    {
                        Base64CertificateData = Convert.ToBase64String(certData)
                    };
                    var trustedCertStr = JsonHelper.SerializeObject(trustedCert);

                    ExecuteCommand(Method.Post, "TrustedCertificates", trustedCertStr);
                }
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

        public void InstallClientCertificate(ClientCertificatePfx certificate)
        {
            using (var memoryStream = new MemoryStream())
            {
                certificate.file.OpenReadStream().CopyTo(memoryStream);
                var cert = certificate.passphrase == null ? new X509Certificate2(memoryStream.ToArray()) : new X509Certificate2(memoryStream.ToArray(), certificate.passphrase);

                if (cert.HasPrivateKey)
                {
                    _configDb.UserCertificate = cert;
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
                            _configDb.UserCertificateBase64Data = Convert.ToBase64String(pubPrivEphemeral.Export(X509ContentType.Pfx));
                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Failed to import the certificate: {ex.Message}";
                        _logger.Error(msg);
                        throw new DevOpsException(msg);
                    }
                }
            }
        }

        public void RemoveClientCertificate()
        {
            _configDb.UserCertificate = null;
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

        //DELETE ME.  Just for Development
        public void CreateA2AUser()
        {
            if (_connectionContext == null)
                throw new DevOpsException("Not logged in");

            CreateA2ACertificateUser();
            AddTrustedCertificate();
        }

        public Safeguard GetSafeguardData()
        {
            if (string.IsNullOrEmpty(_configDb.SafeguardAddress))
                return null;
            return ConnectAnonymous(_configDb.SafeguardAddress, _configDb.ApiVersion ?? DefaultApiVersion, _configDb.IgnoreSsl ?? false);
        }

        public Safeguard SetSafeguardData(SafeguardData safeguardData)
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

        public void DeleteSafeguardData()
        {
            _configDb.SafeguardAddress = null;
            _configDb.ApiVersion = null;
            _configDb.IgnoreSsl = null;
        }

        public ManagementConnection GetConnection()
        {
            return _connectionContext;
        }

        public ManagementConnection Connect(ManagementConnectionData connectionData)
        {
            ConnectWithAccessToken(connectionData);
            return _connectionContext;
            // TODO: errors?
        }

        public void Disconnect()
        {
            DisconnectWithAccessToken();
        }

        public void Dispose()
        {
            DisconnectWithAccessToken();
        }
    }
}
