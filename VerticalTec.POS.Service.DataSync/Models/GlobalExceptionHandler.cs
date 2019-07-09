using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;

namespace VerticalTec.POS.Service.DataSync.Models
{
    public class GlobalExceptionHandler : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            var msg = actionExecutedContext.Exception.Message;
            var body = new ResponseBody<string>()
            {
                HttpCode = HttpStatusCode.InternalServerError,
                Message = msg
            };
            actionExecutedContext.Response = actionExecutedContext.Request.CreateResponse(HttpStatusCode.InternalServerError, body);
        }
    }
}