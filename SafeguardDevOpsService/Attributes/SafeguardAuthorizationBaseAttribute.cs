using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class SafeguardAuthorizationBaseAttribute : Attribute
    {
    }
}
