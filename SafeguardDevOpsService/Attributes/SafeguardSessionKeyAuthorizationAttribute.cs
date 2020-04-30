using Microsoft.AspNetCore.Mvc.Filters;
using OneIdentity.DevOps.Authorization;
using OneIdentity.DevOps.Logic;
using OneIdentity.SafeguardDotNet;

namespace OneIdentity.DevOps.Attributes
{
    public class SafeguardSessionKeyAuthorizationAttribute : SafeguardAuthorizationBaseAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var sessionKey = GetSessonKey(context);
            var managementConnection = AuthorizedCache.Instance.Find(sessionKey);
            if (managementConnection == null)
            {
                context.Result = new DevOpsUnauthorizedResult("Authorization Failed: No authenticated session found");
                return;
            }

            var sppToken = GetSppToken(context);
            if (sppToken == null || !managementConnection.AccessToken.ToInsecureString().Equals(sppToken))
            {
                context.Result = new DevOpsUnauthorizedResult("Authorization Failed: Invalid or missing SPP token");
                return;
            }

            var service = (ISafeguardLogic)context.HttpContext.RequestServices.GetService(typeof(ISafeguardLogic));
            if (!service.ValidateLogin(sppToken, true))
            {
                context.Result = new DevOpsUnauthorizedResult("Authorization Failed: Invalid token");
                return;
            }

            context.HttpContext.Items.Add("spp-token", sppToken);
            context.HttpContext.Items.Add("session-key", sessionKey);
        }
    }
}
