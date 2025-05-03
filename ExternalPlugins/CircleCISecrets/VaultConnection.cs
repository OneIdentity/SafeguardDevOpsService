using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace OneIdentity.DevOps.CircleCISecrets
{
    internal class VaultConnection
    {
        private bool _disposed;

        private readonly HttpClient _vaultClient;
        private readonly string _credential;
        private readonly ILogger _logger;
        private readonly Uri _connectionUrl;


        public VaultConnection(string connectionUrl, string credential, ILogger logger)
        {
            _credential = credential;
            _logger = logger;
            _connectionUrl = new Uri(connectionUrl, UriKind.Absolute);

            var handler = new HttpClientHandler();

            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            
            _vaultClient = new HttpClient(handler);
        }

        public FullResponse InvokeMethodFull(HttpMethod method, string relativeUrl, string body = null, IDictionary<string, string> parameters = null,
            IDictionary<string, string> additionalHeaders = null, TimeSpan? timeout = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("VaultConnection");

            if (string.IsNullOrEmpty(relativeUrl))
                throw new ArgumentException("Parameter may not be null or empty", nameof(relativeUrl));

            relativeUrl = AddQueryParameters(relativeUrl, parameters);

            var retry = false;
            Retry:

            var req = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(_connectionUrl, relativeUrl),
            };

            if (_credential != null)
                req.Headers.Add("Circle-Token", _credential);

            if (additionalHeaders != null && !additionalHeaders.ContainsKey("Accept"))
                req.Headers.Add("Accept", "application/json");

            if (additionalHeaders != null)
            {
                foreach (var header in additionalHeaders)
                    req.Headers.Add(header.Key, header.Value);
            }

            if ((method == HttpMethod.Post || method == HttpMethod.Put) && body != null)
                req.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");

            var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(100)); // 100 seconds is the default timeout.

            if (!retry)
            {
                LogRequestDetails(method, req.RequestUri, additionalHeaders);
                _logger.Debug("  Body size: {RequestBodySize}", body == null ? "None" : $"{body.Length}");
            }

            try
            {
                using var res = _vaultClient.SendAsync(req, cts.Token).GetAwaiter().GetResult();
                var msg = res.Content?.ReadAsStringAsync().GetAwaiter().GetResult();

                if (!retry && res.StatusCode == HttpStatusCode.TemporaryRedirect)
                {
                    res.Dispose();
                    req.Dispose();
                    cts.Dispose();

                    retry = true;
                    goto Retry;
                }

                if (!res.IsSuccessStatusCode)
                {
                    throw new Exception($"Error returned from Safeguard API, Error: {res.StatusCode} {msg}");
                }

                var fullResponse = new FullResponse
                {
                    StatusCode = res.StatusCode,
                    Headers = new Dictionary<string, string>(),
                    Body = msg
                };

                foreach (var header in res.Headers)
                {
                    if (fullResponse.Headers.ContainsKey(header.Key))
                    {
                        if (header.Value.Any())
                        {
                            fullResponse.Headers[header.Key] = string.Join(", ", fullResponse.Headers[header.Key], string.Join(", ", header.Value));
                        }
                    }
                    else
                    {
                        fullResponse.Headers.Add(header.Key, string.Join(", ", header.Value));
                    }
                }

                LogResponseDetails(fullResponse);

                return fullResponse;
            }
            catch (TaskCanceledException)
            {
                throw new Exception($"Request timeout to {req.RequestUri}.");
            }
            finally
            {
                req.Dispose();
                cts.Dispose();
            }
        }

        internal void LogRequestDetails(HttpMethod method, Uri uri, IDictionary<string, string> additionalHeaders = null)
        {
            _logger.Debug("Invoking method: {Method} {Endpoint}", method.ToString().ToUpper(),
                uri);

            if (additionalHeaders != null)
            {
                var h = additionalHeaders.Where(kv =>
                    !kv.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) 
                    || !kv.Key.Equals("X-Vault-Token", StringComparison.OrdinalIgnoreCase));
                _logger.Debug("  Additional headers: {AdditionalHeaders}", h.Select(kv => $"{kv.Key}: {kv.Value}")
                    .Aggregate("", (str, header) => $"{str}{header}, ").TrimEnd(',', ' ') ?? "None");
            }
        }

        internal void LogResponseDetails(FullResponse fullResponse)
        {
            _logger.Debug("Response status code: {StatusCode}", fullResponse.StatusCode);
            _logger.Debug("  Response headers: {ResponseHeaders}",
                fullResponse.Headers?.Select(kv => $"{kv.Key}: {kv.Value}")
                    .Aggregate("", (str, header) => $"{str}{header}, ").TrimEnd(',', ' '));
            _logger.Debug("  Body size: {ResponseBodySize}",
                fullResponse.Body == null ? "None" : $"{fullResponse.Body.Length}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // anything to do here
                _disposed = true;
            }
        }

        private static string AddQueryParameters(string url, IDictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return url;
            }

            var sb = new StringBuilder(url ?? string.Empty);

            // Try to be compensating with an existing Url, if it were to be passed in with an existing query string.
            if (!url.Contains('?'))
            {
                sb.Append('?');
            }
            else if (!url.EndsWith("&"))
            {
                sb.Append('&');
            }

            foreach (var item in parameters)
            {
                sb.Append($"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}&");
            }

            sb.Length -= 1; // Remove the last '&' character.

            return sb.ToString();
        }
    }

    internal class FullResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
    }
}
