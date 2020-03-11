using System;
using System.Linq;

namespace OneIdentity.DevOps.Extensions
{
    public static class ExceptionExtensions
    {
        public static Exception FlattenException(this Exception This)
        {
            var ae = This as AggregateException;
            if (ae == null)
            {
                return This;
            }

            var e = ae.Flatten().InnerExceptions.FirstOrDefault(x => !(x is AggregateException));
            return e ?? This;
        }
    }
}