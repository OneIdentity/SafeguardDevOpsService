using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Authorization
{
    public class SafeguardTokenAuthorizationAttribute : SafeguardAuthorizationBaseAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var sppToken = GetSppToken(context);

            var service = (ISafeguardLogic)context.HttpContext.RequestServices.GetService(typeof(ISafeguardLogic));
            if (!service.ValidateLogin(sppToken))
            {
                context.Result = new DevOpsUnauthorizedResult("Authorization Failed: Invalid token");
                return;
            }

            var managementConnection = AuthorizedCache.Instance.FindByToken(sppToken);
            context.HttpContext.Response.Cookies.Append("sessionKey", managementConnection.SessionKey);
        }
    }
}
