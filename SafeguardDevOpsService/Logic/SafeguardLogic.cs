using System;
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
                _logger.Error($"Failed to connect to Safeguard at '{_configDb.SafeguardAddress}': {ex.Message}");
                // TODO: return error?
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

        public X509Certificate2 GetX509Certificate(string thumbPrint)
        {
            X509Store store = new X509Store("My", StoreLocation.CurrentUser);

            try
            {
                store.Open((OpenFlags.ReadOnly));

                var spsCerts = store.Certificates.OfType<X509Certificate2>().ToArray();
                var cert = spsCerts.FirstOrDefault(x => x.Thumbprint != null && x.Thumbprint.Equals(thumbPrint, StringComparison.InvariantCultureIgnoreCase));
                return cert;
            }
            catch (Exception ex)
            {
                var msg = $"Unknown error reading the cert store. {ex.Message}";
                _logger.Error(ex, msg);
                throw new DevOpsException(msg);
            }
            finally
            {
                store.Close();
            }
        }

        public ClientCertificate GetClientCertificate(string thumbPrint)
        {
            var cert = GetX509Certificate(thumbPrint);

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

        public void InstallClientCertificate(ClientCertificatePfx certificatePfx)
        {
            X509Store store = new X509Store("My", StoreLocation.CurrentUser);

            try { 
                using (var memoryStream = new MemoryStream())
                {
                    certificatePfx.file.OpenReadStream().CopyTo(memoryStream);
                    var cert = new X509Certificate2(memoryStream.ToArray(), certificatePfx.passphrase);

                    store.Open((OpenFlags.ReadWrite));
                    store.Add(cert);
                }
            }
            catch (Exception ex)
            {
                var msg = $"Unknown error installing the client certificate. {ex.Message}";
                _logger.Error(ex, msg);
                throw new DevOpsException(msg);
            }
            finally
            {
                store.Close();
            }
        }

        public void RemoveClientCertificate(string thumbPrint)
        {
            var cert = GetX509Certificate(thumbPrint);

            if (cert != null)
            {
                X509Store store = new X509Store("My", StoreLocation.CurrentUser);

                try
                {
                    store.Open((OpenFlags.ReadWrite));
                    store.Remove(cert);
                }
                catch (Exception ex)
                {
                    var msg = $"Unknown error removing the client certificate. {ex.Message}";
                    _logger.Error(ex, msg);
                    throw new DevOpsException(msg);
                }
                finally
                {
                    store.Close();
                }
            }
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
