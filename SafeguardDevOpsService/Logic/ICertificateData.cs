
namespace OneIdentity.DevOps.Logic
{
    internal interface ICertificateData
    {
        string Base64Certificate { get; }

        string Base64Pfx { get; }

        string Password { get; }

        string PemEncodedCertificate { get; }

        string PemEncodedEncryptedPrivateKey { get; }

        string PemEncodedUnencryptedPrivateKey { get; }
    }
}
