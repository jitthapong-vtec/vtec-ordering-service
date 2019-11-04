using System;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class GlobalExceptionHandler : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            var msg = actionExecutedContext.Exception.Message;
            var body = new ErrorDetail()
            {
                Message = msg
            };
            actionExecutedContext.Response = actionExecutedContext.Request.CreateResponse(HttpStatusCode.InternalServerError, body);
        }
    }
}