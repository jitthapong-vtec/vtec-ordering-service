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
        private string _apiUser;
        private string _apiPassword;

        public BasicAuthenActionFilter()
        {
            _apiUser = AppConfig.Instance.ApiUser;
            _apiPassword = AppConfig.Instance.ApiPass;
        }

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            if (string.IsNullOrEmpty(_apiUser) || string.IsNullOrEmpty(_apiPassword))
                return;

            var auth = actionContext.Request.Headers.Authorization;
            if (auth == null)
            {
                actionContext.Response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                return;
            }

            var cred = ASCIIEncoding.ASCII.GetString(Convert.FromBase64String(auth.Parameter)).Split(':');
            var user = new { Name = cred[0], Pass = cred[1] };
            if (user.Name == _apiUser && user.Pass == _apiPassword)
                return;
            else
                actionContext.Response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        }
    }
}
