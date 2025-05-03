﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        public static bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors, Serilog.ILogger logger, IConfigurationRepository configDb, 
            IEnumerable<TrustedCertificate> customTrustedCertificateList = null)
        {
            try
            {
                var cert2 = new X509Certificate2(certificate);

                if (HasExpired(cert2, logger))
                {
                    return false;
                }

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

                // if the certificate is self-signed, then the certificate must be in the trusted certificate list.
                if (IsSelfSigned(cert2))
                {
                    var result = trustedCertificates.Any(x => x.Thumbprint.Equals(cert2.Thumbprint));
                    if (!result)
                        logger.Debug("The self-signed certificate is not found in the trusted certificate list.");

                    return result;
                }

                // If there is chain provided then walk the chain to see if any of those certificate are in the trusted list.
                if (chain != null)
                {
                    logger.Debug($"Chain certificates count = {chain.ChainElements.Count}");
                    var i = 0;

                    // Make sure that a certificate in the chain exists in the trusted certificate list, excluding the SSL certificate itself.
                    foreach (var chainCert in chain.ChainElements)
                    {
                        logger.Debug($"[{i}] - subject = {chainCert.Certificate.SubjectName.Name}");
                        logger.Debug($"[{i}] - issuer = {chainCert.Certificate.IssuerName.Name}");
                        logger.Debug($"[{i}] - thumbprint = {chainCert.Certificate.Thumbprint}");

                        // Skip checking the first certificate since it is not part of the trust chain.
                        if ((i > 0) &&
                            !trustedCertificates.Any(x => x.Thumbprint.Equals(chainCert.Certificate.Thumbprint)))
                        {
                            logger.Error("Failed SPP SSL certificate validation. Maybe missing a trusted certificate.");
                            return false;
                        }

                        i++;
                    }

                    return true;
                }

                // If this is a client certificate and there isn't a chain, then check of the issuer is in the trusted certificate list.
                if (HasEku(cert2, "1.3.6.1.5.5.7.3.2"))
                {
                    if (trustedCertificates.Any(x => x.GetCertificate().SubjectName.RawData.SequenceEqual(cert2.IssuerName.RawData)))
                    {
                        return true;
                    }
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
            var certSubjectName = "CN=localhost"; // WellKnownData.DevOpsServiceDefaultWebSslCertificateSubject;

            using (RSA rsa = RSA.Create(certSize))
            {
                var certificateRequest = new CertificateRequest(certSubjectName, rsa,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                certificateRequest.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false));

                certificateRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                        true));

                // Server authentication.
                // https://oidref.com/1.3.6.1.5.5.7.3.1
                certificateRequest.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection
                        {
                            new Oid("1.3.6.1.5.5.7.3.1")
                        },
                        true));

                certificateRequest.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false));

                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddDnsName("localhost");
                sanBuilder.AddIpAddress(IPAddress.Loopback);
                var sanBuilt = sanBuilder.Build();

                certificateRequest.CertificateExtensions.Add(
                    new X509SubjectAlternativeNameExtension(sanBuilt.RawData));

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

            if (HasExpired(sslCertificate, logger))
            {
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
                    // No addition attributes need to be checked to add a trusted certificate.
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

        private static bool HasExpired(X509Certificate2 cert, Serilog.ILogger logger)
        {
            var curDate = DateTime.UtcNow;
            if (curDate < cert.NotBefore || curDate > cert.NotAfter)
            {
                var format = "MM/dd/yyyy HH:mm:ss z";
                logger.Error("Certificate is expired.");
                logger.Error(
                    $"\tCurrent Time: {curDate.ToString(format)} Not Before: {cert.NotBefore.ToString(format)} Not After: {cert.NotAfter.ToString(format)}");
                return true;
            }

            return false;
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
