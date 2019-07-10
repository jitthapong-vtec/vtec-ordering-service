using System;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;

namespace VerticalTec.POS.WebService.DataSync.Models
{
    public class GlobalExceptionHandler : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            var msg = actionExecutedContext.Exception.Message;
            var body = new ResponseBody<Exception>()
            {
                Data = actionExecutedContext.Exception,
                Message = msg
            };
            actionExecutedContext.Response = actionExecutedContext.Request.CreateResponse(HttpStatusCode.InternalServerError, body);
        }
    }
}