using System.Web.Http;
using VerticalTec.POS.Service.Ordering.Owin.Services;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    [RoutePrefix("RCAgent")]
    public class AOTRCAgentController : ApiController
    {
        private AOTRCAgentService _rcAgentService;

        public AOTRCAgentController(AOTRCAgentService rcAgentService)
        {
            _rcAgentService = rcAgentService;
        }

        [HttpGet]
        [Route("SendLoginStatus")]
        public IHttpActionResult SendLoginStatus()
        {
            var loginResp = _rcAgentService.SendLoginStatus();
            return Ok(loginResp);
        }

        [HttpGet]
        [Route("SendLogoutStatus")]
        public IHttpActionResult SendLogoutStatus()
        {
            var loginResp =_rcAgentService.SendLogoutStatus();
            return Ok(loginResp);
        }

        [HttpGet]
        [Route("TestRequestRcCode")]
        public IHttpActionResult TestRequestRcCode()
        {
            var resp = _rcAgentService.RequestRcCode(new OrderTransaction());
            return Ok(resp);
        }

        [HttpGet]
        [Route("TestConfirmPrinttRcCode")]
        public IHttpActionResult TestConfirmPrinttRcCode(string rcCode)
        {
            var resp = _rcAgentService.ConfirmPrintRcCode(rcCode);
            return Ok(resp);
        }
    }
}
