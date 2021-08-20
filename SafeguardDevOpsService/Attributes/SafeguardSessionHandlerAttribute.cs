using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using OneIdentity.DevOps.Logic;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Attributes
{
    public class SafeguardSessionHandlerAttribute : ResultFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            var sppToken = AttributeHelper.GetSppToken(context.HttpContext);
            var managementConnection = AuthorizedCache.Instance.FindByToken(sppToken);
            if (managementConnection != null)
                context.HttpContext.Response.Cookies.Append("sessionKey", managementConnection.SessionKey, new CookieOptions(){Secure = true});
        }
    }
}
