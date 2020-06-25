using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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

                var response = devOpsException.ResponseMessage;
                var errorMessage = devOpsException.Message;
                var message = $"Executed action: {context.HttpContext.Request.Method} {context.HttpContext.Request.Path} = {context.Exception?.GetType().FullName}: {(int)(response?.StatusCode ?? HttpStatusCode.InternalServerError)} {(response?.StatusCode ?? HttpStatusCode.InternalServerError).ToString()}\r\n{errorMessage}";
                Serilog.Log.Logger.Error(message);
            }

            context.ExceptionHandled = true;
        }
    }
}
