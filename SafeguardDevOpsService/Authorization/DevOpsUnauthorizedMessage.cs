﻿#pragma warning disable 1591

namespace OneIdentity.DevOps.Authorization
{
    public class DevOpsUnauthorizedMessage
    {
        public string Message { get; }

        public DevOpsUnauthorizedMessage(string message)
        {
            Message = message;
        }
    }
}
