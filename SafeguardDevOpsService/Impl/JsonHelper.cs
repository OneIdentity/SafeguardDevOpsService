using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneIdentity.SafeguardDevOpsService.Exceptions;

namespace OneIdentity.SafeguardDevOpsService.Impl
{
    public class JsonHelper
    {
        public static T DeserializeObject<T>(string rawJson) where T : class
        {
            T deserializedObject = JsonConvert.DeserializeObject<T>(rawJson,
                new JsonSerializerSettings
                {
                    Error = HandleDeserializationError,
                    DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
                });

            if (deserializedObject == null)
            {
                throw new DevOpsException("Deserialization failed");
            }
            return deserializedObject;
        }

        public static string SerializeObject<T>(T scbObject, bool ignoreNull = false) where T : class
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            JsonSerializerSettings jsonSettings = new JsonSerializerSettings
            {
                Error = HandleDeserializationError
            };
            jsonSettings.NullValueHandling = ignoreNull ? NullValueHandling.Ignore : NullValueHandling.Include;

            var rawJson = JsonConvert.SerializeObject(scbObject, jsonSettings);

            if (rawJson == null)
            {
                throw new DevOpsException("Serialization failed");
            }

            return rawJson;
        }

        public static void HandleDeserializationError(object sender, ErrorEventArgs errorArgs)
        {
            var currentError = errorArgs.ErrorContext.Error.Message;
            Debug.WriteLine(currentError);
            errorArgs.ErrorContext.Handled = true;
        }

    }
}
