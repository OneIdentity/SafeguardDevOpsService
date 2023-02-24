using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using RestSharp;
using Serilog;

namespace OneIdentity.DevOps.HashiCorpVault
{
    internal class VaultConnection
    {
        private bool _disposed;

        private readonly RestClient _vaultClient;
        private readonly string _credential;
        private readonly ILogger _logger;


        public VaultConnection(string connectionUrl, string credential, ILogger logger)
        {
            _credential = credential;
            _logger = logger;

            _vaultClient = new RestClient(connectionUrl);

            _vaultClient.RemoteCertificateValidationCallback += (sender, certificate, chain, errors) => true;
        }

        public FullResponse InvokeMethodFull(Method method, string relativeUrl, string body = null, IDictionary<string, string> parameters = null,
            IDictionary<string, string> additionalHeaders = null, TimeSpan? timeout = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("VaultConnection");

            if (string.IsNullOrEmpty(relativeUrl))
                throw new ArgumentException("Parameter may not be null or empty", nameof(relativeUrl));

            var request = new RestRequest(relativeUrl, method);

            if (_credential != null)
                request.AddHeader("X-Vault-Token", _credential);

            if (additionalHeaders != null && !additionalHeaders.ContainsKey("Accept"))
                request.AddHeader("Accept", "application/json"); // Assume JSON unless specified
            if (additionalHeaders != null)
            {
                foreach (var header in additionalHeaders)
                    request.AddHeader(header.Key, header.Value);
            }
            if ((method == Method.POST || method == Method.PUT) && body != null)
                request.AddParameter("application/json", body, ParameterType.RequestBody);
            if (parameters != null)
            {
                foreach (var param in parameters)
                    request.AddParameter(param.Key, param.Value, ParameterType.QueryString);
            }
            if (timeout.HasValue)
            {
                request.Timeout = (timeout.Value.TotalMilliseconds > int.MaxValue)
                    ? int.MaxValue : (int)timeout.Value.TotalMilliseconds;
            }

            LogRequestDetails(method, new Uri(_vaultClient.BaseUrl + $"/{relativeUrl}"), parameters, additionalHeaders);
            
            var response = _vaultClient.Execute(request);
            _logger.Debug("  Body size: {RequestBodySize}", body == null ? "None" : $"{body.Length}");
            if (response.ResponseStatus != ResponseStatus.Completed)
                throw new Exception($"Unable to connect to web service {_vaultClient.BaseUrl}, Error: " + response.ErrorMessage);

            if (response.StatusCode == HttpStatusCode.TemporaryRedirect)
            {
                //Wait 2 seconds and try the request again. The vault sometimes redirects to the same URL.
                Thread.Sleep(TimeSpan.FromSeconds(2));
                response = _vaultClient.Execute(request);
                if (response.ResponseStatus != ResponseStatus.Completed)
                    throw new Exception($"Unable to connect to web service {_vaultClient.BaseUrl}, Error: " + response.ErrorMessage);
            }

            if (!response.IsSuccessful && response.StatusCode != HttpStatusCode.ServiceUnavailable)
                throw new Exception("Error returned from Safeguard API, Error: " + $"{response.StatusCode} {response.Content}");

            var fullResponse = new FullResponse
            {
                StatusCode = response.StatusCode,
                Headers = new Dictionary<string, string>(),
                Body = response.Content
            };
            foreach (var header in response.Headers)
            {
                if (header.Name != null)
                    fullResponse.Headers.Add(header.Name, header.Value?.ToString());
            }
            LogResponseDetails(fullResponse);
            
            return fullResponse;
        }

        internal void LogRequestDetails(Method method, Uri uri, IDictionary<string, string> parameters = null,
            IDictionary<string, string> additionalHeaders = null)
        {
            _logger.Debug("Invoking method: {Method} {Endpoint}", method.ToString().ToUpper(),
                uri);
            //client.BaseUrl + $"/{relativeUrl}");
            _logger.Debug("  Query parameters: {QueryParameters}",
                parameters?.Select(kv => $"{kv.Key}={kv.Value}").Aggregate("", (str, param) => $"{str}{param}&")
                    .TrimEnd('&') ?? "None");

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
    }

    internal class FullResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
    }
}
