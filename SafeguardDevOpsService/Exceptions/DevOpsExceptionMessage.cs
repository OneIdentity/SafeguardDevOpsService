using System;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Exceptions
{
    public class DevOpsExceptionMessage
    {
        public string Message { get; }
        public string StackTrace { get; }

        public DevOpsExceptionMessage(Exception ex)
        {
            Message = ex.Message;
            StackTrace = ex.StackTrace;
        }
    }
}
