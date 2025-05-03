using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Attributes
{
    /// <summary>Helper methods used by the authentication attribute methods.</summary>
    public class AttributeHelper
    {
        /// <summary>Get the Authorization header value from the request.</summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetSppToken(HttpContext context)
        {
            var authHeader = context.Request.Headers.FirstOrDefault(c => c.Key == "Authorization");
            var sppToken = authHeader.Value.ToString();
            if (!sppToken.StartsWith("spp-token ", StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return sppToken.Split(" ").LastOrDefault();
        }

        /// <summary>Get the session key from the cookie of the request.</summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetSessionKey(HttpContext context)
        {
            if (context.Request.Cookies.Keys.Contains(WellKnownData.SessionKeyCookieName))
            {
                return context.Request.Cookies[WellKnownData.SessionKeyCookieName];
            }

            return null;
        }

    }
}
