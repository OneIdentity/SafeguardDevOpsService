using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Logic;
using RestSharp.Extensions;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Attributes
{
    public class AttributeHelper
    {
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

        public static string GetSessionKey(HttpContext context)
        {
            if (context.Request.Cookies.Keys.Contains(WellKnownData.SessionKeyCookieName) &&
                context.Request.Cookies[WellKnownData.SessionKeyCookieName].HasValue())
            {
                return context.Request.Cookies[WellKnownData.SessionKeyCookieName];
            }

            return null;
        }

    }
}
