using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;
using VerticalTec.POS.WebService.Ordering.Models;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.WebService.Ordering.Controllers
{
    [ApiController]
    public class MemberController : ControllerBase
    {
        INLogManager _log;
        IDatabase _database;
        VtecPOSRepo _posRepo;

        public MemberController(IDatabase database, INLogManager log)
        {
            _database = database;
            _log = log;
            _posRepo = new VtecPOSRepo(database);
        }

        [HttpGet("v1/members")]
        public async Task<IActionResult> SearchMemberAsync(string memberCode)
        {
            var result = new CustomActionResult<MemberData>();
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

        [HttpGet("v1/members/thirdparty")]
        public async Task<IActionResult> SearchThirdPartyMemberAsync(string memberId)
        {
            _log.LogInfo($"GetMember: {memberId}");

            var result = new CustomActionResult<object>(Request);

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

        [HttpPost("v1/members/apply")]
        public async Task<IActionResult> ApplyMember(string memberCode, string memberFirstName, string memberLastName, string memberMobile,
            string memberGroupName, string memberEmail, int transactionId, int computerId, int shopId)
        {
            _log.LogInfo($"Apply member: memberCode={memberCode}, memberFirstName={memberFirstName}, memberLastName={memberLastName}, memberMobile={memberMobile}, " +
                $"memberGroupName={memberGroupName}, memberEmail={memberEmail}, transactionId={transactionId}, computerId={computerId}, shopId={shopId}");

            var result = new CustomActionResult<int>();
            using (var conn = await _database.ConnectAsync())
            {
                var responseText = "";
                var memberId = 0;
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
            }
            return result;
        }
    }
}