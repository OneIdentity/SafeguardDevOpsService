using System.Runtime.Serialization;

namespace OneIdentity.DevOps.Extensions
{
    internal static class SerializationInfoExtensions
    {
        public static void AddValue<T>(this SerializationInfo This, string key, T value)
        {
            This.AddValue(key, value, typeof (T));
        }

        public static T GetValue<T>(this SerializationInfo This, string key)
        {
            return (T) This.GetValue(key, typeof (T));
        }
    }
}
