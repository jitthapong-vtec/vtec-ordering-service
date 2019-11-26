using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;
using vtecPOS.POSControl;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    public class PromotionController : ApiController
    {
        static readonly NLog.Logger _log = NLog.LogManager.GetLogger("logpromotion");

        IDatabase _database;
        VtecPOSRepo _posRepo;

        public PromotionController(IDatabase database)
        {
            _database = database;
            _posRepo = new VtecPOSRepo(database);
        }

        [HttpGet]
        [Route("v1/promotions/vouchers")]
        public async Task<IHttpActionResult> GetVoucherAsync(string sn)
        {
            var result = new HttpActionResult<VoucherData>(Request);

            string baseUrl = "";
            using (var conn = await _database.ConnectAsync())
            {
                baseUrl = await _posRepo.GetLoyaltyApiAsync(conn);
            }

            var httpClient = new HttpClient();
            var builder = new UriBuilder($"{baseUrl}LoyaltyApi/Voucher/GetVoucherDataWithVoucherSN?deviceCode=&memberUdid=&voucherSN={sn}");
            var uri = builder.ToString();
            try
            {
                var resp = await httpClient.PostAsync(uri, null);
                if (resp.IsSuccessStatusCode)
                {
                    var respContent = await resp.Content.ReadAsStringAsync();
                    var loyaltyResult = JsonConvert.DeserializeObject<LoyaltyApiResult<object, object, object>>(respContent);
                    if (loyaltyResult.Status == 0)
                    {
                        var voucher = JsonConvert.DeserializeObject<VoucherData>(loyaltyResult.DataResult.ToString());
                        result.StatusCode = HttpStatusCode.OK;
                        result.Body = voucher;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.NotFound;
                        result.Message = loyaltyResult.DataResult.ToString();

                        _log.Error($"GetVoucherDataWithVoucherSN {loyaltyResult.DataResult.ToString()}");
                    }
                }
                else
                {
                    result.StatusCode = HttpStatusCode.NotFound;
                    result.Message = resp.ReasonPhrase;

                    _log.Error($"GetVoucherDataWithVoucherSN {result.Message}");
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = ex.Message;

                _log.Error($"GetVoucherDataWithVoucherSN {ex.Message}");
            }

            return result;
        }

        [HttpPost]
        [Route("v1/promotions/member/apply")]
        public async Task<IHttpActionResult> ApplyMemberPromotionAsync(OrderPromotion orderPromotion)
        {
            _log.Info($"ApplyMember:{JsonConvert.SerializeObject(orderPromotion)}");

            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var myConn = conn as MySqlConnection;
                    var dbUtil = new CDBUtil();
                    string responseText = "";
                    int decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                    var saleDateStr = await _posRepo.GetSaleDateAsync(conn, orderPromotion.ShopID, false);
                    var saleDate = DateTime.ParseExact(saleDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                    var success = LoyaltyManagerLib.LoyaltyManager.Loyalty_MemberApplyPromotion(myConn,
                        dbUtil, orderPromotion.ShopID, orderPromotion.TransactionID, orderPromotion.ComputerID,
                        saleDate, orderPromotion.MemberID, ref responseText);
                    if (success)
                    {
                        var posModule = new POSModule();
                        posModule.OrderDetail_CalBill(ref responseText, orderPromotion.TransactionID, orderPromotion.ComputerID,
                            orderPromotion.ShopID, decimalDigit, "front", conn as MySqlConnection);

                        result.StatusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = responseText;

                        _log.Error($"Apply Member {responseText}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = ex.Message;

                _log.Error($"Apply Member {ex.Message}");
            }
            return result;
        }

        [HttpDelete]
        [Route("v1/promotions/member")]
        public async Task<IHttpActionResult> ClearMemberPromotionAsync(int shopId, int terminalId, int transactionId, int computerId)
        {
            _log.Info($"ClearMember: TransactionID {transactionId}, ComputerID {computerId}");

            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var myConn = conn as MySqlConnection;
                    var dbUtil = new CDBUtil();
                    string responseText = "";
                    int decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);

                    var success = LoyaltyManagerLib.LoyaltyManager.Loyalty_ClearMemberPromotion(myConn, dbUtil, transactionId, computerId, ref responseText);
                    if (success)
                    {
                        var posModule = new POSModule();
                        posModule.OrderDetail_CalBill(ref responseText, transactionId, computerId, shopId, decimalDigit, "front", conn as MySqlConnection);

                        var cmd = _database.CreateCommand("update ordertransactionfront set MemberName=@memberName" +
                                " where TransactionID=@transactionId and ComputerID=@computerId", conn);
                        cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
                        cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
                        cmd.Parameters.Add(_database.CreateParameter("@memberName", ""));
                        await _database.ExecuteNonQueryAsync(cmd);

                        result.StatusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = responseText;

                        _log.Error($"Clear Member {responseText}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = ex.Message;

                _log.Error($"Clear Member {ex.Message}");
            }
            return result;
        }

        [HttpPost]
        [Route("v1/promotions/vouchers/apply")]
        public async Task<IHttpActionResult> ApplyPromotionAsync(OrderPromotion orderPromotion)
        {
            _log.Info($"ApplyVoucher:{JsonConvert.SerializeObject(orderPromotion)}");

            var result = new HttpActionResult<object>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var myConn = conn as MySqlConnection;
                    var dbUtil = new CDBUtil();
                    var posModule = new POSModule();
                    string responseText = "";
                    int decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                    var saleDateStr = await _posRepo.GetSaleDateAsync(conn, orderPromotion.ShopID, false);
                    var saleDate = DateTime.ParseExact(saleDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                    var success = LoyaltyManagerLib.LoyaltyManager.Loyalty_VoucherApplyPromotion(myConn, dbUtil, posModule,
                        orderPromotion.ShopID, orderPromotion.TerminalID, orderPromotion.TransactionID,
                        orderPromotion.ComputerID, saleDate, orderPromotion.StaffID, orderPromotion.VoucherSn, ref responseText);

                    if (success)
                    {
                        posModule.OrderDetail_CalBill(ref responseText, orderPromotion.TransactionID, orderPromotion.ComputerID, orderPromotion.ShopID, decimalDigit, "front", conn as MySqlConnection);

                        result.StatusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = responseText;

                        _log.Error($"Apply Voucher {responseText}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = ex.Message;

                _log.Error($"Apply Voucher {ex.Message}");
            }
            return result;
        }

        [HttpPost]
        [Route("v1/promotions/vouchers/clear")]
        public async Task<IHttpActionResult> ClearPromotionAsync(int shopId, int terminalId, int transactionId, int computerId, int staffId, string voucherSn)
        {
            _log.Info($"ClearVoucher: TransactionID {transactionId}, ComputerID {computerId}, VoucherSn {voucherSn}");

            var result = new HttpActionResult<object>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var myConn = conn as MySqlConnection;
                    var dbUtil = new CDBUtil();
                    var posModule = new POSModule();
                    string responseText = "";
                    int decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                    var saleDateStr = await _posRepo.GetSaleDateAsync(conn, shopId, false);
                    var saleDate = DateTime.ParseExact(saleDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                    var success = LoyaltyManagerLib.LoyaltyManager.Loyalty_RemoveVoucherPromotion(myConn, dbUtil, posModule,
                        shopId, computerId, transactionId, computerId, saleDate, staffId, voucherSn, ref responseText);

                    if (success)
                    {
                        posModule.OrderDetail_CalBill(ref responseText, transactionId, computerId, shopId, decimalDigit, "front", conn as MySqlConnection);

                        result.StatusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = responseText;

                        _log.Error($"Clear voucher {responseText}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = ex.Message;

                _log.Error($"Clear voucher {ex.Message}");
            }
            return result;
        }
    }
}
