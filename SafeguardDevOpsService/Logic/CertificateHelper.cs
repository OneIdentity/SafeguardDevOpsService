using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;
using OneIdentity.DevOps.Data;
using RestSharp.Extensions;

namespace OneIdentity.DevOps.Logic
{
    class CertificateHelper
    {
        public static X509Certificate2 CreateDefaultSSLCertificate()
        {
            int certSize = 2048;
            string certSubjectName = "CN=DevOpsServiceServerSSL";

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
            if (sslCertificate.HasPrivateKey == false)
            {
                logger.Error("No private key found.");
                return false;
            }
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
                case CertificateType.WebSsh:
                    if (!HasUsage(sslCertificate, X509KeyUsageFlags.KeyAgreement) || !HasEku(sslCertificate, "1.3.6.1.5.5.7.3.1"))
                    {
                        logger.Error("Missing key agreement or enhanced key usage server authentication.");
                        return false;
                    }
                    break;
                case CertificateType.A2AClient:
                    if (!HasUsage(sslCertificate, X509KeyUsageFlags.KeyEncipherment) || !HasEku(sslCertificate, "1.3.6.1.5.5.7.3.2"))
                    {
                        logger.Error("Missing key encipherment or enhanced key usage client authentication.");
                        return false;
                    }
                    break;
                default:
                    return false;
            }


            return true;
        }

        private static bool HasUsage(X509Certificate2 cert, X509KeyUsageFlags flag)
        {
            if (cert.Version < 3) { return true; }

            var extensions = cert.Extensions.OfType<X509KeyUsageExtension>().ToList();
            if (!extensions.Any())
            {
                return flag != X509KeyUsageFlags.CrlSign && flag != X509KeyUsageFlags.KeyCertSign;
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
                return true;
            }

            // Otherwise, the extension exists, so we must validate that it contains the we OID we need.
            return eku.EnhancedKeyUsages[oid] != null;
        }

    }
}
