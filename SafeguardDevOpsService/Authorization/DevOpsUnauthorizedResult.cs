using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Authorization
{
    public class DevOpsUnauthorizedResult : JsonResult
    {
        public DevOpsUnauthorizedResult(string message) : base(new DevOpsUnauthorizedMessage(message))
        {
            StatusCode = StatusCodes.Status401Unauthorized;
        }
    }
}
