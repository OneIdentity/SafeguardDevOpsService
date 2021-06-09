using System;
using System.Linq;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Extensions
{
    public static class ExceptionExtensions
    {
        public static Exception FlattenException(this Exception ex)
        {
            var ae = ex as AggregateException;
            if (ae == null)
            {
                return ex;
            }

            var e = ae.Flatten().InnerExceptions.FirstOrDefault(x => !(x is AggregateException));
            return e ?? ex;
        }
    }
}