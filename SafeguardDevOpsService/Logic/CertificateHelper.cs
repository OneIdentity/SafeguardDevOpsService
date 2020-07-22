using System;
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
            var noLabel = Regex.Replace(pem, "-----.*?-----", "",
                RegexOptions.Multiline & RegexOptions.Compiled & RegexOptions.IgnoreCase & RegexOptions.ECMAScript);
            var b64String = Regex.Replace(noLabel, "\r|\n", "",
                RegexOptions.Multiline & RegexOptions.Compiled & RegexOptions.IgnoreCase & RegexOptions.ECMAScript);
            return Convert.FromBase64String(b64String);
        }

        public static bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors, Serilog.ILogger logger, IConfigurationRepository configDb)
        {
            if (configDb.IgnoreSsl ?? false)
                return true;

            var trustedChain = configDb.GetTrustedChain();
            if (trustedChain.ChainPolicy.ExtraStore.Count == 0)
            {
                logger.Error("IgnoreSsl is false and there no trusted certificates have been specified.");
                return false;
            }

            try
            {
                var cert2 = new X509Certificate2(certificate);

                if (chain.ChainElements.Count <= 1)
                {
                    var found = trustedChain.ChainPolicy.ExtraStore.Find(X509FindType.FindByThumbprint, cert2.Thumbprint ?? string.Empty, false);
                    if (found.Count == 1)
                        return true;
                }

                if (!configDb.GetTrustedChain().Build(new X509Certificate2(certificate)))
                {
                    logger.Error("Failed SPP SSL certificate validation.");
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
            if (!HasUsage(sslCertificate, X509KeyUsageFlags.DigitalSignature))
            {
                logger.Error("Missing digital signature usage.");
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
                    if (!IsCa(sslCertificate))
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

        private static bool IsCa(X509Certificate2 cert)
        {
            var extensions = cert.Extensions.OfType<X509BasicConstraintsExtension>().ToList();
            if (extensions.Any() && extensions[0].CertificateAuthority && extensions[0].CertificateAuthority)
            {
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

    }
}
