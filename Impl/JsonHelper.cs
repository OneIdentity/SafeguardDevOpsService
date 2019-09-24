using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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
                throw new Exception("Deserialization failed");
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
                throw new Exception("Serialization failed");
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
