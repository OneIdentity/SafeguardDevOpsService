using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.DevOps.Extensions;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Attributes
{
    /// <summary>
    /// Convert unhandled exceptions to a web response
    /// </summary>
    public class UnhandledExceptionErrorAttribute : ExceptionFilterAttribute
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.Logger;

        public override void OnException(ExceptionContext context)
        {
            if (context.Exception.FlattenException() is DevOpsException devOpsException)
            {
                context.Result = new JsonResult(new
                {
                    devOpsException.Message,
                    devOpsException.StackTrace,
                });

                var errorMessage = devOpsException.Message;
                var message = $"Executed action: {context.HttpContext.Request.Method} {context.HttpContext.Request.Path} = {context.Exception?.GetType().FullName}: {(int)devOpsException.Status} {devOpsException.Status.ToString()}\r\n{errorMessage}";
                _logger.Error(message);

                context.Result = new DevOpsExceptionResult(context.Exception, devOpsException.Status);
            }
            else
            {
                context.Result = new DevOpsExceptionResult(context.Exception, HttpStatusCode.InternalServerError);
            }

            context.ExceptionHandled = true;
        }
    }
}
