using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OneIdentity.DevOps.Exceptions;
using OneIdentity.DevOps.Extensions;

namespace OneIdentity.DevOps.Attributes
{
//    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
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

//             string message;
//
//             if (context.Exception.FlattenException() is DevOpsException devOpsException)
//             {
//                 var response = devOpsException.ResponseMessage;
//                 string errorMessage;
//                 // if (response != null)
//                 // {
//                 //     response.Headers.Remove(HttpHeaderNames.Server); // remove so it doesn't get duplicated by system
//                 //
//                 //     try
//                 //     {
//                 //         var apiError = AsyncHelper.RunSync(response.Content.ReadAsAsync<ApiError>); // strip out extra data
//                 //         var newResponse = context.Request.CreateResponse(response.StatusCode, apiError);
//                 //         context.Exception = new HttpResponseException(newResponse);
//                 //
//                 //         errorMessage = $"{JsonConvert.SerializeObject(apiError)}";
//                 //     }
//                 //     catch
//                 //     {
//                 //         context.Exception = new HttpResponseException(response);
//                 //         errorMessage = AsyncHelper.RunSync(response.Content.ReadAsStringAsync);
//                 //     }
//                 // }
//                 // else
//                 // {
//                     context.Exception = devOpsException;
//                     errorMessage = devOpsException.Message;
// //                }
//
//                 message = $"Executed action: {context.Request.Method.Method} {context.Request.RequestUri} = {context.Exception?.GetType().FullName}: {(int)(response?.StatusCode ?? HttpStatusCode.InternalServerError)} {(response?.StatusCode ?? HttpStatusCode.InternalServerError).ToString()}\r\n{errorMessage}";
//                 _logger.Error(message);
//             }
//
//             if (context.Exception is HttpResponseException)
//                 return;
//
//             var ex = GetExceptionForDetail(context);
//             var request = context.Request;
// //            var culture = request.CultureInfo();
//
//             // var t = ApiError.GetErrorDetail(ex, culture);
//             // context.Response = request.CreateResponse(t.Item1, t.Item2);
//
//             // if (context.Response.Content != null)
//             // {
//             //     context.Response.Content.Headers.Remove(HttpHeaderNames.ContentLanguage);
//             //     context.Response.Content.Headers.Add(HttpHeaderNames.ContentLanguage, culture.Name);
//             // }
//
//             message = $"Executed action: {context.Request.Method.Method} {context.Request.RequestUri} = {context.Exception?.GetType().FullName}: {(int)context.Response.StatusCode} {context.Response.StatusCode.ToString()}\r\n{context.Exception?.Message}";
//             if (context.Response.StatusCode == HttpStatusCode.InternalServerError)
//             {
//                 _logger.Error(context.Exception, message);
//             }
//             else
//             {
//                 _logger.Warning(context.Exception, message);
//             }
        }

        // private static Exception GetExceptionForDetail(HttpActionExecutedContext context)
        // {
        //     if (context.Exception == null)
        //         return null;
        //
        //     var exception = context.Exception;
        //     if (exception.InnerException != null && exception is TargetInvocationException)
        //         exception = exception.InnerException;
        //
        //     return exception;
        // }
    }
}
