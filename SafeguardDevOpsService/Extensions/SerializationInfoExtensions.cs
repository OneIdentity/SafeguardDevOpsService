using System.Runtime.Serialization;

namespace OneIdentity.DevOps.Extensions
{
    internal static class SerializationInfoExtensions
    {
        public static void AddValue<T>(this SerializationInfo info, string key, T value)
        {
            info.AddValue(key, value, typeof (T));
        }

        public static T GetValue<T>(this SerializationInfo info, string key)
        {
            return (T) info.GetValue(key, typeof (T));
        }
    }
}
