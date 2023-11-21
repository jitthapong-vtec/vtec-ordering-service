using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace VerticalTec.POS.Service.Ordering.Owin
{
    public class BasicAuthenActionFilter : ActionFilterAttribute
    {
        //public override void OnActionExecuting(HttpActionContext actionContext)
        //{
        //    var auth = actionContext.Request.Headers.Authorization;
        //    if (auth == null)
        //    {
        //        actionContext.Response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        //        return;
        //    }

        //    var cred = ASCIIEncoding.ASCII.GetString(Convert.FromBase64String(auth.Parameter)).Split(':');
        //    var user = new { Name = cred[0], Pass = cred[1] };
        //    if (user.Name == "nec" && user.Pass == "vtecsystem")
        //        return;
        //    else
        //        actionContext.Response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        //}
    }
}
