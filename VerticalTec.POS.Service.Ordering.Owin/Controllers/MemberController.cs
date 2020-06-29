using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    public class MemberController : ApiController
    {
        static readonly NLog.Logger _log = NLog.LogManager.GetLogger("logmember");
        IDatabase _database;
        VtecPOSRepo _posRepo;

        public MemberController(IDatabase database)
        {
            _database = database;
            _posRepo = new VtecPOSRepo(database);
        }

        [HttpGet]
        [Route("v1/members")]
        public async Task<IHttpActionResult> SearchMemberAsync(string memberCode)
        {
            var result = new HttpActionResult<MemberData>(Request);
            if (string.IsNullOrEmpty(memberCode))
            {
                result.StatusCode = HttpStatusCode.BadRequest;
                result.Message = "memberCode can't be empty!";
                return result;
            }

            if (string.IsNullOrEmpty(memberCode))
            {
                result.StatusCode = HttpStatusCode.BadRequest;
                result.Message = "memberCode can't be empty!";
                return result;
            }

            string baseUrl = "";
            using (var conn = await _database.ConnectAsync())
            {
                baseUrl = await _posRepo.GetLoyaltyApiAsync(conn);

                var httpClient = new HttpClient();
                var builder = new UriBuilder(baseUrl + $"LoyaltyApi/Member/GetMemberFromMemberCode?deviceCode=&memberUdid=&memberCode={memberCode}");
                var uri = builder.ToString();
                try
                {
                    var resp = await httpClient.PostAsync(uri, null);
                    if (resp.IsSuccessStatusCode)
                    {
                        var content = await resp.Content.ReadAsStringAsync();
                        var loyaltyResult = JsonConvert.DeserializeObject<LoyaltyApiResult<object, object, object>>(content);
                        if (loyaltyResult.Status == 0)
                        {
                            MemberData member = JsonConvert.DeserializeObject<MemberData>(loyaltyResult.DataResult.ToString());
                            await _posRepo.InsertUpdateMemberAsync(conn, member);

                            result.StatusCode = HttpStatusCode.OK;
                            result.Body = member;
                        }
                        else
                        {
                            result.StatusCode = HttpStatusCode.NotFound;
                            result.Message = loyaltyResult.DataResult.ToString();
                        }
                    }
                    else
                    {
                        result.StatusCode = resp.StatusCode;
                        result.Message = resp.ReasonPhrase;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is TaskCanceledException)
                    {
                        result.StatusCode = HttpStatusCode.RequestTimeout;
                        result.Message = $"Can't connect to loyalty server.";
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = ex.Message;
                    }
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/members/thirdparty")]
        public async Task<IHttpActionResult> SearchThirdPartyMemberAsync(string memberId)
        {
            _log.Info($"GetMember: {memberId}");

            var result = new HttpActionResult<object>(Request);

            var baseUrl = "";
            using (var conn = await _database.ConnectAsync())
            {
                baseUrl = await _posRepo.GetPropertyValueAsync(conn, 1025, "MemberWebServiceUrl");

                if (string.IsNullOrEmpty(baseUrl))
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = "Not found member api url (prop 1025)";
                }
                else
                {
                    var memberConfig = new LoyaltyGateWayLib.MemberGateWayConfig();
                    memberConfig.baseUrl = baseUrl;

                    var memberData = new LoyaltyGateWayLib.MemberData();
                    string responseText = "";
                    var success = LoyaltyGateWayLib.MemberGateWayLib.Member_GetMemberData(2, memberConfig, memberId, ref memberData, ref responseText);
                    if (!success)
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = responseText;
                    }
                    result.Body = memberData;
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/members/apply")]
        public async Task<IHttpActionResult> ApplyMember(int memberId, string memberCode, string memberFirstName, string memberLastName, string memberMobile,
            string memberGroupName, string memberEmail, int transactionId, int computerId, int shopId)
        {
            _log.Info($"Apply member: memberCode={memberCode}, memberFirstName={memberFirstName}, memberLastName={memberLastName}, memberMobile={memberMobile}, " +
               $"memberGroupName={memberGroupName}, memberEmail={memberEmail}, transactionId={transactionId}, computerId={computerId}, shopId={shopId}");

            var result = new HttpActionResult<int>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var responseText = "";
                var saleDate = $"'{await _posRepo.GetSaleDateAsync(conn, shopId, false)}'";
                var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                var posModule = new POSModule();
                var success = posModule.Member_Bakmi(ref responseText, ref memberId, memberCode, memberFirstName, memberLastName, memberMobile, memberGroupName,
                    memberEmail, transactionId, computerId, shopId, saleDate, "front", decimalDigit, conn as MySqlConnection);
                if (!success)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = responseText;
                }
                result.Body = memberId;
                _log.Info($"Apply member successfully {memberId}");
            }
            return result;
        }
    }
}
