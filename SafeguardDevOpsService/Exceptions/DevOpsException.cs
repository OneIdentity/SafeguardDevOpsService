using System;
using System.Net;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Common
{
    [Serializable]
    public class DevOpsException : Exception
    {

        public HttpStatusCode Status;

        public DevOpsException(string message, HttpStatusCode status = HttpStatusCode.BadRequest) : base(message)
        {
            Status = status;
        }

        public DevOpsException(string message, Exception ex, HttpStatusCode status = HttpStatusCode.BadRequest) : base(message, ex)
        {
            Status = status;
        }

    }
}
