using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.DevOps.Extensions;

namespace OneIdentity.DevOps.Logic
{
    internal class CertificateData : ICertificateData, IDisposable
    {
        private readonly X509Certificate2 cert;
        private bool disposedValue;
        private static readonly PbeParameters PbeParameters = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000);

        public CertificateData(string certB64, string password)
        {
            Password = password;
            cert = CertificateExtensions.LoadFromBytes(Convert.FromBase64String(certB64), password, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        }

        public string Base64Certificate => Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        public string Base64Pfx => Convert.ToBase64String(cert.Export(X509ContentType.Pfx, Password));

        public string Password { get; set; }

        public string PemEncodedCertificate => GetPemValue("CERTIFICATE", cert.RawData);

        public string PemEncodedEncryptedPrivateKey
        {
            get
            {
                var pk = cert.GetRSAPrivateKey();
                if (pk == null)
                {
                    throw new DevOpsException("Certificate is missing the private key.");
                }

                return GetPemValue("ENCRYPTED PRIVATE KEY", pk.ExportEncryptedPkcs8PrivateKey(Password, PbeParameters));
            }
        }

        public string PemEncodedUnencryptedPrivateKey
        {
            get
            {
                using var tmp = RSA.Create();
                var pk = cert.GetRSAPrivateKey();
                if (pk == null)
                {
                    throw new DevOpsException("Certificate is missing the private key.");
                }

                var key = pk.ExportEncryptedPkcs8PrivateKey(Password, PbeParameters);
                tmp.ImportEncryptedPkcs8PrivateKey(Encoding.UTF8.GetBytes(Password), key, out _);
                return GetPemValue("PRIVATE KEY", tmp.ExportPkcs8PrivateKey());
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cert.Dispose();
                }

                disposedValue = true;
            }
        }

        private static string GetPemBeginLabel(string label)
        {
            return $"-----BEGIN {label.ToUpper(CultureInfo.CurrentCulture)}-----\r\n";
        }

        private static string GetPemEndLabel(string label)
        {
            return $"\r\n-----END {label.ToUpper(CultureInfo.CurrentCulture)}-----";
        }

        private static string GetPemValue(string label, byte[] buffer)
        {
            var b64String = Convert.ToBase64String(buffer);
            var stringLength = b64String.Length;
            var sb = new StringBuilder();

            for (int i = 0, count = 0; i < stringLength; i++, count++)
            {
                if (count > 63)
                {
                    sb.Append("\r\n");
                    count = 0;
                }

                sb.Append(b64String[i]);
            }

            return GetPemBeginLabel(label) + sb + GetPemEndLabel(label);
        }
    }
}
