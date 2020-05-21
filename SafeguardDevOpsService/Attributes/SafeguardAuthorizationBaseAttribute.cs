using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using OneIdentity.DevOps.Authorization;
using RestSharp.Extensions;

namespace OneIdentity.DevOps.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class SafeguardAuthorizationBaseAttribute : Attribute
    {
        public string GetSppToken(AuthorizationFilterContext context)
        {
            var authHeader = context.HttpContext.Request.Headers.FirstOrDefault(c => c.Key == "Authorization");
            var sppToken = authHeader.Value.ToString();
            if (!sppToken.StartsWith("spp-token ", StringComparison.InvariantCultureIgnoreCase))
            {
                context.Result = new DevOpsUnauthorizedResult("Authorization Failed: Missing token");
                return null;
            }

            return sppToken.Split(" ").LastOrDefault();
        }

        public string GetSessonKey(AuthorizationFilterContext context)
        {
            if (context.HttpContext.Request.Cookies.Keys.Contains("SessionKey") &&
                context.HttpContext.Request.Cookies["SessionKey"].HasValue())
            {
                return context.HttpContext.Request.Cookies["SessionKey"];
            }

            context.Result = new DevOpsUnauthorizedResult("Authorization Failed: Missing session key");
            return null;
        }

    }
}
