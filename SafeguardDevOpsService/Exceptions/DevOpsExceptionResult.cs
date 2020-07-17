using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Exceptions
{
    public class DevOpsExceptionResult : JsonResult
    {
        public DevOpsExceptionResult(Exception ex, HttpStatusCode status) : base(new DevOpsExceptionMessage(ex))
        {
            StatusCode = (int)status;
        }
    }
}
