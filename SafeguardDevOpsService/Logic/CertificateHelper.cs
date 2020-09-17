using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using Serilog.Core;


namespace OneIdentity.DevOps.Logic
{
    internal class CertificateHelper
    {
        public static byte[] ConvertPemToData(string pem)
        {
            var noLabel = Regex.Replace(pem, "-----.*?-----", "",
                RegexOptions.Multiline & RegexOptions.Compiled & RegexOptions.IgnoreCase & RegexOptions.ECMAScript);
            var b64String = Regex.Replace(noLabel, "\r|\n", "",
                RegexOptions.Multiline & RegexOptions.Compiled & RegexOptions.IgnoreCase & RegexOptions.ECMAScript);
            return Convert.FromBase64String(b64String);
        }

        public static bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors, Serilog.ILogger logger, IConfigurationRepository configDb)
        {
            var trustedChain = configDb.GetTrustedChain();
            if (trustedChain.ChainPolicy.ExtraStore.Count == 0)
            {
                logger.Error("IgnoreSsl is false and there no trusted certificates have been specified.");
                return false;
            }

            try
            {
                var cert2 = new X509Certificate2(certificate);

                var sans = GetSubjectAlternativeName(new X509Certificate2(certificate), logger);
                var safeguardAddress = configDb.SafeguardAddress;
                if (!sans.Exists(x => x.Equals(safeguardAddress, StringComparison.InvariantCultureIgnoreCase) ||
                                      (x.StartsWith("*") && safeguardAddress.Substring(safeguardAddress.IndexOf('.'))
                                          .Equals(x.Substring(1), StringComparison.InvariantCultureIgnoreCase))))
                {
                    logger.Debug("Failed to find a matching subject alternative name.");
                    return false;
                }

                if (chain.ChainElements.Count <= 1)
                {
                    var found = trustedChain.ChainPolicy.ExtraStore.Find(X509FindType.FindByThumbprint, cert2.Thumbprint ?? string.Empty, false);
                    if (found.Count == 1)
                        return true;
                }

                logger.Debug($"Trusted certificates count = {trustedChain.ChainPolicy.ExtraStore.Count}");
                var i = 0;
                foreach (var trusted in trustedChain.ChainPolicy.ExtraStore)
                {
                    logger.Debug($"[{i}] - subject = {trusted.SubjectName.Name}");
                    logger.Debug($"[{i}] - issuer = {trusted.IssuerName.Name}");
                    logger.Debug($"[{i}] - thumbprint = {trusted.Thumbprint}");
                    i++;
                }

                if (!trustedChain.Build(new X509Certificate2(certificate)))
                {
                    logger.Error("Failed SPP SSL certificate validation.");
                    var chainStatus = trustedChain.ChainStatus;
                    for (i = 0; i < chainStatus.Length; i++)
                    {
                        logger.Debug($"[{i}] - chain status = {chainStatus[i].StatusInformation}");
                    }
                    i = 0;
                    foreach (var chainElement in trustedChain.ChainElements)
                    {
                        logger.Debug($"[{i}] - subject = {chainElement.Certificate.SubjectName.Name}");
                        logger.Debug($"[{i}] - issuer = {chainElement.Certificate.IssuerName.Name}");
                        logger.Debug($"[{i}] - thumbprint = {chainElement.Certificate.Thumbprint}");
                        i++;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed SPP SSL certificate validation: {ex.Message}");
                return false;
            }

            return true;
        }

        public static X509Certificate2 CreateDefaultSslCertificate()
        {
            var certSize = 2048;
            var certSubjectName = "CN=DevOpsServiceServerSSL";

            using (RSA rsa = RSA.Create(certSize))
            {
                var certificateRequest = new CertificateRequest(certSubjectName, rsa,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                certificateRequest.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false));

                certificateRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                        true));

                certificateRequest.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection
                        {
                            new Oid("1.3.6.1.5.5.7.3.1")
                        },
                        true));

                certificateRequest.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false));

                return certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(10));
            }
        }

        public static bool ValidateCertificate(X509Certificate2 sslCertificate, CertificateType certificateType)
        {
            var logger = Serilog.Log.Logger;

            if (sslCertificate == null)
            {
                logger.Error("Certificate was null.");
                return false;
            }

            var curDate = DateTime.UtcNow;
            if (curDate < sslCertificate.NotBefore || curDate > sslCertificate.NotAfter)
            {
                logger.Error("Certificate is expired.");
                return false;
            }

            switch (certificateType)
            {
                case CertificateType.WebSsl:
                    if (sslCertificate.HasPrivateKey == false)
                    {
                        logger.Error("No private key found.");
                        return false;
                    }
                    // key agreement is used in diffe-hellman ciphers, key encipherment is used in traditional ssl handshake key exchange
                    if (!HasUsage(sslCertificate, X509KeyUsageFlags.KeyAgreement) && !HasUsage(sslCertificate, X509KeyUsageFlags.KeyEncipherment))
                    {
                        logger.Error("Must have key usage for key agreement or key encipherment.");
                        return false;
                    }
                    // require server authentication EKU
                    if (!HasEku(sslCertificate, "1.3.6.1.5.5.7.3.1"))
                    {
                        logger.Error("Must have extended key usage for server authentication.");
                        return false;
                    }
                    break;
                case CertificateType.A2AClient:
                    if (sslCertificate.HasPrivateKey == false)
                    {
                        logger.Error("No private key found.");
                        return false;
                    }
                    // key agreement is used in diffe-hellman ciphers, key encipherment is used in traditional ssl handshake key exchange
                    if (!HasUsage(sslCertificate, X509KeyUsageFlags.KeyAgreement) && !HasUsage(sslCertificate, X509KeyUsageFlags.KeyEncipherment))
                    {
                        logger.Error("Must have key usage for key agreement or key encipherment.");
                        return false;
                    }
                    // require server authentication EKU
                    if (!HasEku(sslCertificate, "1.3.6.1.5.5.7.3.2"))
                    {
                        logger.Error("Must have extended key usage for client authentication.");
                        return false;
                    }
                    break;
                case CertificateType.Trusted:
                    if (!IsCa(sslCertificate) && !IsSelfSigned(sslCertificate))
                    {
                        logger.Error("Not a certificate authority.");
                        return false;
                    }
                    break;
                default:
                    return false;
            }

            return true;
        }

        private static bool IsSelfSigned(X509Certificate2 cert)
        {
            return cert.SubjectName.RawData.SequenceEqual(cert.IssuerName.RawData);
        }

        private static bool IsCa(X509Certificate2 cert)
        {
            var basic = cert.Extensions.OfType<X509BasicConstraintsExtension>().ToList();
            if (basic.Any() && basic[0].CertificateAuthority && basic[0].CertificateAuthority)
            {
                var ku = cert.Extensions.OfType<X509KeyUsageExtension>().ToList();
                if (!ku.Any())
                {
                    return true; // if KUs aren't present, then don't require CRL sign and key sign cert
                }
                return HasUsage(cert, X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign);
            }
            return false;
        }

        private static bool HasUsage(X509Certificate2 cert, X509KeyUsageFlags flag)
        {
            if (cert.Version < 3) { return true; }

            var extensions = cert.Extensions.OfType<X509KeyUsageExtension>().ToList();
            if (!extensions.Any())
            {
                return false;
            }
            return (extensions[0].KeyUsages & flag) > 0;
        }

        private static bool HasEku(X509Certificate2 cert, string oid)
        {
            // A certificate with version less than 3 doesn't support extensions at all.  Therefore, applications won't use them to
            // verify/enforce any restrictions that those extensions may be used for.  Essentially, the certificate can be used for
            // anything.  For example, if a version 3 certificate is used for HTTPS/SSL and the certificate doesn't contain the
            // "Server Authentication" Enhanced Key Usage, then the web browser will show an error page and not let the user proceed.
            // But if a version 1 certificate is used for HTTPS/SSL, it will work just fine, as the browser doesn't validate/enforce
            // any restrictions... because they don't exist.
            if (cert.Version < 3) { return true; }

            // RFC 5280, 4.2: https://tools.ietf.org/html/rfc5280#section-4.2
            // "...A certificate MUST NOT include more than one instance of a particular extension."
            var eku = cert.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();

            // RFC 5280, 4.2.1.12: https://tools.ietf.org/html/rfc5280#section-4.2.1.12
            // If the extension is present, then the certificate MUST only be used for one of the purposes indicated. If multiple
            // purposes are indicated the application need not recognize all purposes indicated, as long as the intended purpose
            // is present. Certificate using applications MAY require that the extended key usage extension be present and that a
            // particular purpose be indicated in order for the certificate to be acceptable to that application.
            if (eku == null)
            {
                return true; // DevOps requires that the desired EKU exist
            }

            // Otherwise, the extension exists, so we must validate that it contains the we OID we need.
            return eku.EnhancedKeyUsages[oid] != null;
        }

        private static List<string> GetSubjectAlternativeName(X509Certificate2 cert, Serilog.ILogger logger)
        {
            var result = new List<string>();

            var subjectAlternativeName = cert.Extensions.Cast<X509Extension>()
                .Where(n => n.Oid.Value== "2.5.29.17") //n.Oid.FriendlyName=="Subject Alternative Name")
                .Select(n => new AsnEncodedData(n.Oid, n.RawData))
                .Select(n => n.Format(true))
                .FirstOrDefault();


            if (subjectAlternativeName != null)
            {
                var alternativeNames = subjectAlternativeName.Split(new[] { "\r\n", "\r", "\n", "," }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var alternativeName in alternativeNames)
                {
                    logger.Debug($"Found subject alternative name: {alternativeName}");
                    var groups = Regex.Match(alternativeName, @"^(.*)[=,:](.*)").Groups; // @"^DNS Name=(.*)").Groups;

                    if (groups.Count > 0 && !String.IsNullOrEmpty(groups[2].Value))
                    {
                        result.Add(groups[2].Value);
                    }
                }
            }

            return result;
        }
    }
}
