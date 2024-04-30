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
        public IHttpActionResult SendLoginStatus(int shopId, int computerId, int staffId)
        {
            var loginResp = _rcAgentService.SendLoginStatus(shopId, computerId, staffId);
            return Ok(loginResp);
        }

        [HttpGet]
        [Route("SendLogoutStatus")]
        public IHttpActionResult SendLogoutStatus(int shopId, int computerId, int staffId)
        {
            var loginResp =_rcAgentService.SendLogoutStatus(shopId, computerId, staffId);
            return Ok(loginResp);
        }

        [HttpGet]
        [Route("RequestRcCode")]
        public IHttpActionResult RequestRcCode(int shopId, int transactionId, int computerId)
        {
            var resp = _rcAgentService.RequestRcCode(shopId, transactionId, computerId);
            return Ok(resp);
        }

        [HttpGet]
        [Route("ConfirmPrintRcCode")]
        public IHttpActionResult ConfirmPrintRcCode(string rcCode)
        {
            var resp = _rcAgentService.ConfirmPrintRcCode(rcCode);
            return Ok(resp);
        }

        [HttpGet]
        [Route("GetLatestAnnouncements")]
        public IHttpActionResult GetLatestAnnouncements()
        {
            var resp = _rcAgentService.GetLatestAnnouncements();
            return Ok(resp);
        }
    }
}
