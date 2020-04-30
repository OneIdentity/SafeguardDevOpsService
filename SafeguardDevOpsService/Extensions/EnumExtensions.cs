using System;
using System.Collections.Generic;
using System.Linq;

namespace OneIdentity.DevOps.Extensions
{
    public static class EnumExtensions
    {
        public static string GetName(this Enum This)
        {
            return Enum.GetName(This.GetType(), This);
        }
    }
    public class Enum<T> where T : struct, IConvertible
    {
        public static IEnumerable<T> Values
        {
            get
            {
                if (!typeof(T).IsEnum)
                    throw new ArgumentException("T must be an enumerated type");

                return Enum.GetValues(typeof(T)).Cast<T>();
            }
        }
    }
}