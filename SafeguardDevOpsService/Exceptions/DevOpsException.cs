using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json.Linq;
using OneIdentity.SafeguardDevOpsService.Extensions;

namespace OneIdentity.SafeguardDevOpsService.Exceptions
{
    [Serializable]
    public class DevOpsException : Exception
    {

        internal static string RequestBodyBytesKey = "RequestBodyBytes";

        [NonSerialized]
        private readonly HttpResponseMessage _responseMessage;

        internal DevOpsException(string message, HttpResponseMessage responseMessage) : base(message)
        {
            _responseMessage = CloneMessage(responseMessage);
        }

        internal DevOpsException(HttpResponseMessage responseMessage)
        {
            _responseMessage = CloneMessage(responseMessage);
        }

        internal HttpRequestMessage RequestMessage => ResponseMessage?.RequestMessage;

        public HttpResponseMessage ResponseMessage => _responseMessage;
        

        private HttpResponseMessage CloneMessage(HttpResponseMessage message)
        {
            if (message == null) { return null; }
            var msg = new HttpResponseMessage(message.StatusCode)
            {
                ReasonPhrase = message.ReasonPhrase,
                Version = message.Version,
                RequestMessage = CloneMessage(message.RequestMessage)
            };
            if (message.Content != null)
            {
                var content = message.Content.ReadAsStringAsync().Result;
//                msg.Content = IsJson(content) ? new CapturedJsonContent(content) : new CapturedStringContent(content, Encoding.UTF8, "text/html");
            }

            msg.Headers.Clear();
            foreach (var kv in message.Headers.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
            {
                msg.Headers.TryAddWithoutValidation(kv.Key, (kv.Value ?? Enumerable.Empty<string>()).ToList());
            }

            return msg;
        }

        private HttpRequestMessage CloneMessage(HttpRequestMessage message)
        {
            if (message == null) { return null; }
            var msg = new HttpRequestMessage(message.Method, message.RequestUri)
            {
                Version = message.Version
            };

            object requestBytes;
            if (message.Properties.TryGetValue(RequestBodyBytesKey, out requestBytes) && requestBytes is byte[])
            {
                msg.Content = new ByteArrayContent((byte[]) requestBytes);
            }

            msg.Properties.Clear();
            foreach (var kv in message.Properties.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
            {
                if (string.Equals(kv.Key, RequestBodyBytesKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var valueNs = kv.Value?.GetType().Namespace;
                if (valueNs != null && valueNs.StartsWith("Flurl", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                msg.Properties[kv.Key] = kv.Value;
            }

            msg.Headers.Clear();
            foreach (var kv in message.Headers.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
            {
                msg.Headers.TryAddWithoutValidation(kv.Key, (kv.Value ?? Enumerable.Empty<string>()).ToList());
            }

            return msg;
        }



        private const string ContentKey = "content";

        private const string ExistsKey = "exists";

        private const string HeadersKey = "headers";

        private const string MethodKey = "method";

        private const string PropertiesKey = "properties";

        private const string ReasonPhraseKey = "reasonphrase";

        private const string StatusCodeKey = "statuscode";

        private const string VersionKey = "version";

        private const string UriKey = "uri";


        public DevOpsException(string message) : base(message)
        {
        }

        protected DevOpsException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            var responseMessage = new HttpResponseMessage();
            var responseExists = info.GetBoolean(Response(ExistsKey));
            if (responseExists)
            {
                var headers = info.GetValue<Dictionary<string, List<string>>>(Response(HeadersKey));
                if (headers != null && headers.Any())
                {
                    foreach (var hkv in headers)
                    {
                        responseMessage.Headers.TryAddWithoutValidation(hkv.Key, hkv.Value);
                    }
                }
                responseMessage.StatusCode = info.GetValue<HttpStatusCode>(Response(StatusCodeKey));
                responseMessage.ReasonPhrase = info.GetString(Response(ReasonPhraseKey));
                responseMessage.Version = info.GetValue<Version>(Response(VersionKey));
                responseMessage.Content = new ByteArrayContent(info.GetValue<byte[]>(Response(ContentKey)));
            }

            var requestExists = info.GetBoolean(Request(ExistsKey));
            if (requestExists)
            {
                var requestMessage = new HttpRequestMessage();
                var headers = info.GetValue<Dictionary<string, List<string>>>(Request(HeadersKey));
                if (headers != null && headers.Any())
                {
                    foreach (var hkv in headers)
                    {
                        requestMessage.Headers.TryAddWithoutValidation(hkv.Key, hkv.Value);
                    }
                }
                var properties = info.GetValue<Dictionary<string, object>>(Request(PropertiesKey));
                if (properties != null && properties.Any())
                {
                    foreach (var pkv in properties)
                    {
                        requestMessage.Properties[pkv.Key] = pkv.Value;
                    }
                }
                requestMessage.Method = new HttpMethod(info.GetString(Request(MethodKey)));
                requestMessage.RequestUri = info.GetValue<Uri>(Request(UriKey));
                requestMessage.Version = info.GetValue<Version>(Request(VersionKey));
                requestMessage.Content = new ByteArrayContent(info.GetValue<byte[]>(Request(ContentKey)));

                responseMessage.RequestMessage = requestMessage;
            }

            _responseMessage = responseMessage;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            var responseExists = ResponseMessage != null;
            info.AddValue(Response(ExistsKey), responseExists);
            if (responseExists)
            {
                info.AddValue(Response(HeadersKey), (ResponseMessage.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()).ToDictionary(x => x.Key, x => x.Value.ToList()));
                info.AddValue(Response(StatusCodeKey), ResponseMessage.StatusCode);
                info.AddValue(Response(ReasonPhraseKey), ResponseMessage.ReasonPhrase);
                info.AddValue(Response(VersionKey), ResponseMessage.Version);
                info.AddValue(Response(ContentKey), ResponseMessage.Content.ReadAsByteArrayAsync().Result);
            }

            var requestExists = RequestMessage != null;
            info.AddValue(Request(ExistsKey), requestExists);
            if (requestExists)
            {
                info.AddValue(Request(HeadersKey), (RequestMessage.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()).ToDictionary(x => x.Key, x => x.Value.ToList()));
                info.AddValue(Request(MethodKey), RequestMessage.Method.Method);
                info.AddValue(Request(UriKey), RequestMessage.RequestUri);
                info.AddValue(Request(VersionKey), RequestMessage.Version);
                info.AddValue(Request(PropertiesKey), RequestMessage.Properties.ToDictionary(x => x.Key, x => x.Value));
                info.AddValue(Request(ContentKey), RequestMessage.Content.ReadAsByteArrayAsync().Result);
            }

            base.GetObjectData(info, context);
        }

        private static string Request(string key) => $"request_{key}";

        private static string Response(string key) => $"response_{key}";
        
        private static bool IsJson(string content)
        {
            try
            {
                JToken.Parse(content);
                return true;
            }
            catch 
            {
                return false;
            }
        }
    }
}
