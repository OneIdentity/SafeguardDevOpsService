using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OneIdentity.DevOps.Common
{
    public class JsonHelper
    {
        public static T DeserializeObject<T>(string rawJson) where T : class
        {
            T dataTransferObject = JsonConvert.DeserializeObject<T>(rawJson,
                new JsonSerializerSettings
                {
                    Error = HandleDeserializationError,
                    DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
                });

            if (dataTransferObject == null)
            {
                throw new DevOpsException("Deserialization failed");
            }
            return dataTransferObject;
        }

        public static string SerializeObject<T>(T dataTransferObject, bool ignoreNull = false) where T : class
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var jsonSettings = new JsonSerializerSettings
            {
                Error = HandleDeserializationError
            };
            jsonSettings.NullValueHandling = ignoreNull ? NullValueHandling.Ignore : NullValueHandling.Include;

            var rawJson = JsonConvert.SerializeObject(dataTransferObject, jsonSettings);

            if (rawJson == null)
            {
                throw new DevOpsException("Serialization failed");
            }

            return rawJson;
        }

        private static void HandleDeserializationError(object sender, ErrorEventArgs errorArgs)
        {
            var currentError = errorArgs.ErrorContext.Error.Message;
            Serilog.Log.Logger.Debug(currentError);
            errorArgs.ErrorContext.Handled = true;
        }
    }
}
