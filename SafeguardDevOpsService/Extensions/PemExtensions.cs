using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace OneIdentity.DevOps.Extensions
{
    internal static class PemExtensions
    {
        public static string AddPemLineBreaks(string b64String)
        {
            var builder = new StringBuilder();
            var stringLength = b64String.Length;
            for (int i = 0, count = 0; i < stringLength; i++, count++)
            {
                if (count > 63)
                {
                    builder.Append("\r\n");
                    count = 0;
                }
                builder.Append(b64String[i]);
            }
            return builder.ToString();
        }

        public static string ToPemFormat(this X509Certificate2 This)
        {
            var b64String = Convert.ToBase64String(This.Export(X509ContentType.Cert), Base64FormattingOptions.None);
            var builder = new StringBuilder()
                .Append("-----BEGIN CERTIFICATE-----")
                .Append(AddPemLineBreaks(b64String))
                .Append("-----END CERTIFICATE-----");
            return builder.ToString();
        }
    }
}
