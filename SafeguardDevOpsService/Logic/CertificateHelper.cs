using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;


namespace OneIdentity.DevOps.Logic
{
    internal class CertificateHelper
    {
        public static byte[] ConvertPemToData(string pem)
        {
            var b64String = ConvertPemToBase64(pem);
            return Convert.FromBase64String(b64String);
        }

        public static string ConvertPemToBase64(string pem)
        {
            var noLabel = Regex.Replace(pem, "-----.*?-----", "",
                RegexOptions.Multiline & RegexOptions.Compiled & RegexOptions.IgnoreCase & RegexOptions.ECMAScript);
            var b64String = Regex.Replace(noLabel, "\r|\n", "",
                RegexOptions.Multiline & RegexOptions.Compiled & RegexOptions.IgnoreCase & RegexOptions.ECMAScript);
            return b64String;
        }

        private static bool WalkTrustChain(X509Certificate2 certificate, TrustedCertificate[] trustedCertificates)
        {
            if (IsSelfSigned(certificate) && IsCa(certificate))
                return true;

            var issuedBy = trustedCertificates.FirstOrDefault(x => x.Subject.Equals(certificate.Issuer));
            if (issuedBy != null)
            {
                return WalkTrustChain(issuedBy.GetCertificate(), trustedCertificates);
            }

            return false;
        }

        public static bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors, Serilog.ILogger logger, IConfigurationRepository configDb, 
            IEnumerable<TrustedCertificate> customTrustedCertificateList = null)
        {
            try
            {
                var cert2 = new X509Certificate2(certificate);

                // If the certificate has the server authentication eku then make sure that the sans match.
                if (HasEku(cert2, "1.3.6.1.5.5.7.3.1"))
                {
                    var sans = GetSubjectAlternativeName(cert2, logger);
                    var safeguardAddress = configDb.SafeguardAddress;
                    if (!sans.Exists(x => x.Equals(safeguardAddress, StringComparison.InvariantCultureIgnoreCase) ||
                                          (x.StartsWith("*") && safeguardAddress.Substring(safeguardAddress.IndexOf('.'))
                                              .Equals(x.Substring(1), StringComparison.InvariantCultureIgnoreCase))))
                    {
                        logger.Debug("Failed to find a matching subject alternative name.");
                        return false;
                    }
                }

                var trustedCertificates = (customTrustedCertificateList ?? configDb.GetAllTrustedCertificates()).ToArray();

                // if the certificate is self-signed and a CA then it must match a trusted certificate in the list.
                if (IsSelfSigned(cert2) && IsCa(cert2))
                {
                    var result = trustedCertificates.Any(x => x.Thumbprint.Equals(cert2.Thumbprint));
                    if (!result)
                        logger.Debug("The self-signed certificate is not found in the trusted certificate list.");

                    return result;
                }

                // If there is no chain provided then just walk the certificate structure to validate the chain.
                if (chain == null)
                {
                    return WalkTrustChain(cert2, trustedCertificates);
                }

                logger.Debug($"Trusted certificates count = {chain.ChainElements.Count}");
                var i = 0;

                // Make sure that all of the certificates in the chain, excluding the SSL certificate itself, are trusted.
                foreach (var trusted in chain.ChainElements)
                {
                    logger.Debug($"[{i}] - subject = {trusted.Certificate.SubjectName.Name}");
                    logger.Debug($"[{i}] - issuer = {trusted.Certificate.IssuerName.Name}");
                    logger.Debug($"[{i}] - thumbprint = {trusted.Certificate.Thumbprint}");

                    // Skip checking the first certificate since it is not part of the trust chain.
                    if ((i > 0) &&
                        !trustedCertificates.Any(x => x.Thumbprint.Equals(trusted.Certificate.Thumbprint)))
                    {
                        logger.Error("Failed SPP SSL certificate validation. Maybe missing a trusted certificate.");
                        return false;
                    }

                    i++;
                }

                // Make sure that the last certificate in the chain is a self-signed CA.  We will deal with a certificate signing authority later.
                if (i > 0)
                {
                    var lastCertificate = chain.ChainElements[i - 1];
                    var result = IsSelfSigned(lastCertificate.Certificate) && IsCa(lastCertificate.Certificate);
                    if (!result)
                        logger.Debug("A valid certificate chain was not provided by the remote application.");

                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed SPP SSL certificate validation: {ex.Message}");
                return false;
            }

            return false;
        }

        public static X509Certificate2 CreateDefaultSslCertificate()
        {
            var certSize = 2048;
            var certSubjectName = WellKnownData.DevOpsServiceDefaultWebSslCertificateSubject;

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
                        logger.Error("Web SSL Certificate is missing key agreement and key encipherment for key usage.");
                        return false;
                    }
                    // require server authentication EKU
                    if (!HasEku(sslCertificate, "1.3.6.1.5.5.7.3.1"))
                    {
                        logger.Error("Web SSL Certificate is missing server authentication for enhanced key usage.");
                        return false;
                    }
                    break;
                case CertificateType.A2AClient:
                    if (sslCertificate.HasPrivateKey == false)
                    {
                        logger.Error("The A2A client certificate is missing the private key found.");
                        return false;
                    }
                    // key agreement is used in diffe-hellman ciphers, key encipherment is used in traditional ssl handshake key exchange
                    if (!HasUsage(sslCertificate, X509KeyUsageFlags.KeyAgreement) && !HasUsage(sslCertificate, X509KeyUsageFlags.KeyEncipherment))
                    {
                        logger.Error("The A2A client certificate is missing key agreement and key encipherment for key usage.");
                        return false;
                    }
                    // require server authentication EKU
                    if (!HasEku(sslCertificate, "1.3.6.1.5.5.7.3.2"))
                    {
                        logger.Error("The A2A client certificate is missing client authentication for enhanced key usage.");
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

        public static bool ValidateTrustChain(X509Certificate2 certificate, IConfigurationRepository configDb, Serilog.ILogger logger)
        {
            return CertificateValidation(null, certificate, null, SslPolicyErrors.None, logger, configDb);
        }

        private static bool IsSelfSigned(X509Certificate2 cert)
        {
            return cert.SubjectName.RawData.SequenceEqual(cert.IssuerName.RawData);
        }

        public static bool IsCa(X509Certificate2 cert)
        {
            var basic = cert.Extensions.OfType<X509BasicConstraintsExtension>().ToList();
            if (basic.Any() && basic[0].CertificateAuthority)
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
                .Where(n => n.Oid.Value == "2.5.29.17") //n.Oid.FriendlyName=="Subject Alternative Name")
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
