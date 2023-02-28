using DevExpress.XtraEditors.Controls;
using Hangfire;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using ResCenterObjLib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Service.Ordering.Owin.Services;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;
using static vtecPOS.GlobalFunctions.LoyaltyObj;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    public class PaymentController : ApiController
    {
        static readonly NLog.Logger _log = NLog.LogManager.GetLogger("logpayment");

        IDatabase _database;
        IPaymentService _paymentService;
        IOrderingService _orderingService;
        IMessengerService _messenger;
        IPrintService _printService;
        VtecPOSRepo _posRepo;

        public PaymentController(IDatabase database, IPaymentService paymentService,
            IOrderingService orderingService, IMessengerService messenger,
            IPrintService printService)
        {
            _database = database;
            _paymentService = paymentService;
            _orderingService = orderingService;
            _messenger = messenger;
            _printService = printService;
            _posRepo = new VtecPOSRepo(database);
        }

        #region OnlinePayment
        [HttpPost]
        [Route("v1/payments/online/qr")]
        public async Task<IHttpActionResult> GetQrCodeAsync(OnlinePaymentRequest req)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var saleDate = await _posRepo.GetSaleDateAsync(conn, req.ShopId, false);

                var cmd = _database.CreateCommand(
                "select a.ShopKey, a.ShopCode, a.ShopName, b.MerchantKey, c.BrandKey from shop_data a join merchant_data b on a.MerchantID=b.MerchantID join brand_data c on a.MerchantID=c.MerchantID where a.ShopID=@shopId and a.Deleted=0;" +
                "select * from weborder_token where SaleDate=@saleDate;", conn);

                cmd.Parameters.Add(_database.CreateParameter("@shopId", req.ShopId));
                cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));

                var ds = new DataSet();
                var adapter = _database.CreateDataAdapter(cmd);
                adapter.TableMappings.Add("Table", "ShopData");
                adapter.TableMappings.Add("Table1", "WebOrderToken");
                adapter.Fill(ds);

                var dtShopData = ds.Tables["ShopData"];
                var dtWebOrderToken = ds.Tables["WebOrderToken"];

                if (dtShopData.Rows.Count == 0)
                    throw new VtecPOSException($"Not found shop data {req.ShopId}");

                var shopData = dtShopData.AsEnumerable().First();
                var merchantKey = shopData.GetValue<string>("MerchantKey");
                var brandKey = shopData.GetValue<string>("BrandKey");
                var shopKey = shopData.GetValue<string>("ShopKey");
                var shopCode = shopData.GetValue<string>("ShopCode");
                var shopName = shopData.GetValue<string>("ShopName");

                var reqId = "";
                var reqToken = "";

                using (var httpClient = new HttpClient())
                {
                    var baseUrl = await _posRepo.GetPlatformApiAsync(conn);
                    httpClient.BaseAddress = new Uri(baseUrl);

                    if (dtWebOrderToken?.Rows.Count > 0)
                    {
                        var row = dtWebOrderToken.ToEnumerable().FirstOrDefault();
                        reqId = row.GetValue<string>("MerchantReqId");
                        reqToken = row.GetValue<string>("AuthenToken");
                    }

                    if (string.IsNullOrEmpty(reqId))
                    {
                        reqId = Guid.NewGuid().ToString();
                    }

                    var merchantUrl = $"api/MerchantInfo/MerchantInfo?reqId={reqId}&WebUrl={merchantKey}";
                    var propertyUrl = $"api/POSModule/PropertyData?reqId={reqId}";

                    if (string.IsNullOrEmpty(reqToken))
                    {
                        try
                        {
                            reqToken = await _orderingService.GetPlatformApiTokenAsync(httpClient);
                        }
                        catch (Exception ex)
                        {
                            throw new VtecPOSException(ex.Message);
                        }
                    }
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {reqToken}");

                    var merchantResponse = await httpClient.GetAsync(merchantUrl);
                    if (!merchantResponse.IsSuccessStatusCode)
                        throw new VtecPOSException($"GetMerchant {merchantResponse.ReasonPhrase}");

                    var propertyResponse = await httpClient.PostAsync(propertyUrl, null);
                    if (!propertyResponse.IsSuccessStatusCode)
                        throw new VtecPOSException($"GetProperty {propertyResponse.ReasonPhrase}");

                    var qrPayload = new
                    {
                        shopKey = shopKey,
                        shopID = req.ShopId,
                        shopCode = shopCode,
                        shopName = shopName,
                        computerID = req.ComputerId,
                        tranKey = $"{req.TransactionId}:{req.ComputerId}",
                        tranUUID = $"{req.TransactionId}:{req.ComputerId}",
                        saleDate = saleDate,
                        staffID = req.StaffId,
                        staffName = "",
                        paymentGatewayType = req.PaymentGatewayType,
                        edcType = req.EdcType,
                        customerCode = req.CustomerCode,
                        payAmount = req.PayAmount.ToString()
                    };

                    var reqJson = JsonConvert.SerializeObject(qrPayload);
                    var content = new StringContent(reqJson, Encoding.UTF8, "application/json");
                    var resp = await httpClient.PostAsync($"api/POSModule/payment_gateway_QR_Request?req_Id={reqId}&langId=1", content);

                    try
                    {
                        resp.EnsureSuccessStatusCode();
                        result.StatusCode = HttpStatusCode.OK;

                        var respStr = await resp.Content.ReadAsStringAsync();
                        result.Body = new
                        {
                            platformApi = baseUrl,
                            reqId = reqId,
                            accessToken = reqToken,
                            saleDate = saleDate,
                            reqData = qrPayload,
                            qrData = JsonConvert.DeserializeObject(respStr)
                        };
                    }
                    catch (HttpRequestException ex)
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = ex.Message;
                    }
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/online/inquiry")]
        public async Task<IHttpActionResult> InquiryAsync(string orderId, OnlinePaymentRequest req)
        {
            var result = new HttpActionResult<object>(Request);
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(req.PlatformApiUrl);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", req.AccessToken);

                var qrPayload = new
                {
                    shopKey = req.ShopKey,
                    shopID = req.ShopId,
                    shopCode = req.ShopCode,
                    shopName = req.ShopName,
                    computerID = req.ComputerId,
                    tranKey = $"{req.TransactionId}:{req.ComputerId}",
                    tranUUID = $"{req.TransactionId}:{req.ComputerId}",
                    saleDate = req.SaleDate,
                    staffID = req.StaffId,
                    staffName = "",
                    paymentGatewayType = req.PaymentGatewayType,
                    edcType = req.EdcType,
                    customerCode = req.CustomerCode,
                    payAmount = req.PayAmount.ToString()
                };

                var reqJson = JsonConvert.SerializeObject(qrPayload);
                var content = new StringContent(reqJson, Encoding.UTF8, "application/json");
                var resp = await httpClient.PostAsync($"api/POSModule/payment_gateway_QR_Inquiry?req_Id={req.ReqId}&orderId={orderId}&langId=1", content);

                try
                {
                    resp.EnsureSuccessStatusCode();
                    result.StatusCode = HttpStatusCode.OK;
                    var apiResp = new
                    {
                        responseCode = "",
                        responseText = "",
                        responseObj = new
                        {
                            order_id = "",
                            merchant_id = "",
                            txn_id = "",
                            status = "",
                            status_code = "",
                            status_message = "",
                            amount = 0,
                            amount_net = 0,
                            amount_cust_fee = 0,
                            currency = "",
                            service_id = "",
                            channel_type = "",
                            ref_1 = "",
                            ref_2 = "",
                            ref_3 = "",
                            ref_4 = "",
                            ref_5 = "",
                            meta_data = "",
                            card = "",
                            installment = (object)null,
                            bank = new
                            {
                                account_last_digits = (object)null,
                                account_name = "",
                                bank_code = "",
                            },
                            rlp = (object)null,
                            rmb_flag = (object)null,
                            sof_txn_id = "",
                            created_at = "",
                            success_at = ""
                        }
                    };
                    var respStr = await resp.Content.ReadAsStringAsync();
                    var respObj = JsonConvert.DeserializeAnonymousType(respStr, apiResp);
                    if (respObj.responseCode == "")
                    {
                        //TODO: Finalize bill
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.OK;
                        result.Body = respObj;
                    }
                }
                catch (HttpRequestException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }
        #endregion

        [HttpGet]
        [Route("v1/payments/checkedctype")]
        public async Task<IHttpActionResult> CheckEDCTypeAsync(int edcType)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                bool isAlreadyHaveEdcType = false;
                var cmd = _database.CreateCommand("select PayTypeID from paytype where EDCType=@edcType", conn);
                cmd.Parameters.Add(_database.CreateParameter("@edcType", edcType));
                using (var reader = await _database.ExecuteReaderAsync(cmd))
                {
                    if (reader.Read())
                    {
                        isAlreadyHaveEdcType = true;
                    }
                }
                if (isAlreadyHaveEdcType)
                {
                    result.StatusCode = HttpStatusCode.OK;
                }
                else
                {
                    result.StatusCode = HttpStatusCode.NotFound;
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/add")]
        public async Task<IHttpActionResult> AddPaymentDetailAsync(PaymentData paymentData)
        {
            _log.Info($"AddPayment => {JsonConvert.SerializeObject(paymentData)}");

            var result = new HttpActionResult<PaymentData>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                await _paymentService.AddPaymentAsync(conn, paymentData);
                result.StatusCode = HttpStatusCode.OK;
                result.Body = paymentData;
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/del")]
        public async Task<IHttpActionResult> DeletePaymentDetailAsync(int transactionId, int computerId, int payDetailId)
        {
            var result = new HttpActionResult<string>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                await _paymentService.DeletePaymentAsync(conn, transactionId, computerId, payDetailId);
                result.StatusCode = HttpStatusCode.OK;
            }
            return result;
        }

        [HttpGet]
        [Route("v1/payments/details")]
        public async Task<IHttpActionResult> GetPaymentDetailAsync(int transactionId, int computerId)
        {
            var result = new HttpActionResult<List<PaymentData>>(Request);
            List<PaymentData> payDetailList = new List<PaymentData>();
            using (var conn = await _database.ConnectAsync())
            {
                DataTable dtPayDetail = await _paymentService.GetPaymentDetailAsync(conn, transactionId, computerId);
                foreach (DataRow row in dtPayDetail.Rows)
                {
                    payDetailList.Add(new PaymentData()
                    {
                        TransactionID = row.GetValue<int>("TransactionID"),
                        ComputerID = row.GetValue<int>("ComputerID"),
                        PayDetailID = row.GetValue<int>("PayDetailID"),
                        PayTypeID = row.GetValue<int>("PayTypeID"),
                        PayAmount = row.GetValue<decimal>("PayAmount"),
                        CurrencyAmount = row.GetValue<decimal>("CurrencyAmount"),
                        PayTypeName = row.GetValue<string>("PayTypeName")
                    });
                }
                result.Body = payDetailList;
                result.StatusCode = HttpStatusCode.OK;
            }
            return result;
        }

        [HttpGet]
        [Route("v1/payments")]
        public async Task<IHttpActionResult> GetPaymentDataAsync(int computerId, int langId = 1, int currencyId = 1)
        {
            var result = new HttpActionResult<DataSet>(Request);
            using (IDbConnection conn = await _database.ConnectAsync())
            {
                try
                {
                    DataSet dataSet = await _paymentService.GetPaymentDataAsync(conn, computerId, langId, currencyId);

                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = dataSet;
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/payments/currencies")]
        public async Task<IHttpActionResult> GetPaymentCurrencyAsync()
        {
            var result = new HttpActionResult<DataTable>(Request);
            using (IDbConnection conn = await _database.ConnectAsync())
            {
                DataTable dtPaymentCurrency = await _paymentService.GetPaymentCurrencyAsync(conn);
                result.StatusCode = HttpStatusCode.OK;
                result.Body = dtPaymentCurrency;
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/finalize")]
        public async Task<IHttpActionResult> FinalizeBillAsync(PaymentData payload)
        {
            _log.Info($"FinalizeBill => {JsonConvert.SerializeObject(payload)}");

            var result = new HttpActionResult<string>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await _paymentService.FinalizeBillAsync(conn, payload.TransactionID, payload.ComputerID,
                        payload.TerminalID, payload.ShopID, payload.StaffID);
                    await _paymentService.FinalizeOrderAsync(conn, payload.TransactionID, payload.ComputerID,
                        payload.TerminalID, payload.ShopID, payload.StaffID, payload.LangID, payload.PrinterIds, payload.PrinterNames);

                    var printData = new PrintData()
                    {
                        TransactionID = payload.TransactionID,
                        ComputerID = payload.ComputerID,
                        ShopID = payload.ShopID,
                        LangID = payload.LangID,
                        PrinterIds = payload.PrinterIds,
                        PrinterNames = payload.PrinterNames,
                        PaperSize = payload.PaperSize
                    };
                    //BackgroundJob.Enqueue<IPrintService>(p => p.PrintBill(printData));
                    await _printService.PrintBill(printData);
                    _messenger.SendMessage();
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/kiosk/del")]
        public async Task<IHttpActionResult> KioskDelPaymentAsync(PaymentData paymentData)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var responseText = "";
                var receiptString = "";
                var saleDate = $"'{await _posRepo.GetSaleDateAsync(conn, paymentData.ShopID, false)}'";
                var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);

                if (paymentData.EDCType != 0)
                {
                    var cmd = _database.CreateCommand("select PayTypeID from paytype where EDCType=@edcType", conn);
                    cmd.Parameters.Add(_database.CreateParameter("@edcType", paymentData.EDCType));
                    using (var reader = await _database.ExecuteReaderAsync(cmd))
                    {
                        if (reader.Read())
                        {
                            paymentData.PayTypeID = reader.GetValue<int>("PayTypeID");
                        }
                    }
                }

                if (paymentData.PayTypeID == 0)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = $"Not found PayType of EDCType {paymentData.EDCType}";
                }
                else
                {
                    var posModule = new POSModule();
                    var success = posModule.Payment_Del(ref responseText, ref receiptString, paymentData.TransactionID, paymentData.ComputerID, paymentData.PayTypeID,
                        paymentData.ShopID, saleDate, paymentData.TerminalID, paymentData.StaffID, paymentData.LangID, "front", decimalDigit, conn as MySqlConnection);
                    if (!success)
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = responseText;
                    }
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/kiosk/add")]
        public async Task<IHttpActionResult> KioskAddPayment(PaymentData paymentData)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var responseText = "";
                var tranDataSet = new DataSet();
                var receiptString = "";
                var saleDate = $"'{await _posRepo.GetSaleDateAsync(conn, paymentData.ShopID, false)}'";
                var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);

                if (paymentData.EDCType != 0)
                {
                    var cmd = _database.CreateCommand("select PayTypeID from paytype where EDCType=@edcType", conn);
                    cmd.Parameters.Add(_database.CreateParameter("@edcType", paymentData.EDCType));
                    using (var reader = await _database.ExecuteReaderAsync(cmd))
                    {
                        if (reader.Read())
                        {
                            paymentData.PayTypeID = reader.GetValue<int>("PayTypeID");
                        }
                    }
                }

                if (paymentData.PayTypeID == 0)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = $"Not found PayType of EDCType {paymentData.EDCType}";
                }
                else
                {
                    var posModule = new POSModule();
                    var success = posModule.Payment_Add(ref responseText, ref tranDataSet, ref receiptString, paymentData.TransactionID, paymentData.ComputerID,
                        paymentData.PayTypeID, paymentData.ShopID, saleDate, paymentData.TerminalID, paymentData.StaffID, paymentData.LangID, "front", decimalDigit, conn as MySqlConnection);
                    if (!success)
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = responseText;
                    }
                    result.Body = tranDataSet;
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/kiosk/confirm")]
        public async Task<IHttpActionResult> KioskConfirmPayment(PaymentData payload)
        {
            var result = new HttpActionResult<PaymentData>(Request);

            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await ConfirmKioskPaymentAsync(conn, payload);
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/grc")]
        public async Task<IHttpActionResult> GrcPaymentGateway(PaymentData payload)
        {
            var result = new HttpActionResult<object>(Request);
            var baseUrl = "";
            var reqTime = DateTime.Now;
            using (var conn = await _database.ConnectAsync())
            {
                baseUrl = await _posRepo.GetPropertyValueAsync(conn, 1117, "BaseUrl");

                var cmd = _database.CreateCommand("update ordertransactionfront set PaidTime=@reqTime where TransactionID=@tranId and ComputerID=@compId", conn);
                cmd.Parameters.Add(_database.CreateParameter("@reqTime", reqTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
                cmd.Parameters.Add(_database.CreateParameter("@tranId", payload.TransactionID));
                cmd.Parameters.Add(_database.CreateParameter("@compId", payload.ComputerID));
                await _database.ExecuteNonQueryAsync(cmd);

                if (!string.IsNullOrEmpty(baseUrl))
                {
                    var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(90);

                    var fintech = "";
                    if (payload.EDCType == 39)
                        fintech = "OVO";

                    var grcPayload = new GrcPayload
                    {
                        OrderID = $"{payload.ReferenceNo}{reqTime.ToString("HHmmss")}",
                        ShopID = payload.ShopID,
                        Amount = Convert.ToInt32(payload.PayAmount),
                        ComputerID = payload.ComputerID,
                        Fintech = fintech,
                        CustomerAccount = payload.CustAccountNo
                    };

                    if (!baseUrl.EndsWith("/"))
                        baseUrl = baseUrl + "/";

                    var grc = new GrcPaymentData();
                    try
                    {
                        _log.Info($"Request GrcPaymentGateway: {JsonConvert.SerializeObject(grcPayload)}");

                        var uri = new UriBuilder($"{baseUrl}pay").ToString();
                        var json = JsonConvert.SerializeObject(grcPayload);
                        var reqContent = new StringContent(json);
                        reqContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        var resp = await httpClient.PostAsync(uri, reqContent);
                        if (resp.IsSuccessStatusCode)
                        {
                            var respContent = await resp.Content.ReadAsStringAsync();
                            grc = JsonConvert.DeserializeObject<GrcPaymentData>(respContent);

                            _log.Info($"Response from {uri}: {JsonConvert.SerializeObject(grc)}");

                            if (grc.response_code == "00")
                            {
                                var edcObject = new EdcObjLib.objCreditCardInfo()
                                {
                                    szTransactionDate = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                                    szTransactionTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                                    szCustAddress = $"{grc.store?.address1} {grc.store?.address2}",
                                    szBalanceAmount = grc.account?.cash_balance,
                                    szCustName = grc.account?.full_name,
                                    szRedeemPoint = grc.account?.ovo_points_used,
                                    szRedeemPointBalance = grc.account?.points_balance,
                                    szApprovalCode = "",
                                    szMerchantNo = "",
                                    szReferenceNo = "",
                                    szAmount = payload.PayAmount.ToString()
                                };
                                payload.ExtraParam = JsonConvert.SerializeObject(edcObject);

                                var tran = new TransactionPayload()
                                {
                                    ShopID = payload.ShopID,
                                    TransactionID = payload.TransactionID,
                                    ComputerID = payload.ComputerID,
                                    TerminalID = payload.ComputerID,
                                    StaffID = payload.StaffID,
                                    PrinterIds = payload.PrinterIds,
                                    PrinterNames = payload.PrinterNames
                                };

                                await _orderingService.SubmitOrderAsync(conn, payload.TransactionID, payload.ComputerID, payload.ShopID, payload.TableID);
                                await _printService.PrintOrder(tran);

                                await ConfirmKioskPaymentAsync(conn, payload);
                                result.Body = grc;
                            }
                            else
                            {
                                var errMsg = $"{grc.response_message}\n{grc.response_description}";
                                if (string.IsNullOrEmpty(grc.response_message))
                                    errMsg = "Unknown error!";

                                result.StatusCode = HttpStatusCode.InternalServerError;
                                result.Message = errMsg;
                                _log.Error($"{baseUrl} Fail: {result.Message}");
                            }
                        }
                        else
                        {
                            result.StatusCode = HttpStatusCode.InternalServerError;
                            result.Message = resp.ReasonPhrase;
                            _log.Error($"{baseUrl} Fail: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var message = ex.Message;
                        if (ex is TaskCanceledException)
                        {
                            message = $"Can't connect to {baseUrl} {ex.Message}";
                            result.StatusCode = HttpStatusCode.GatewayTimeout;
                        }
                        result.Message = message;
                        _log.Error(message);
                    }
                }
                else
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = "Not found GRC Payment Gateway base url (prop 1117)";

                    _log.Error("Not found GRC Payment Gateway base url (prop 1117)");
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/grc/check")]
        public async Task<IHttpActionResult> CheckGrcPayment(PaymentData payload)
        {
            var result = new HttpActionResult<object>(Request);

            var baseUrl = "";
            var orderId = "";
            using (var conn = await _database.ConnectAsync())
            {
                baseUrl = await _posRepo.GetPropertyValueAsync(conn, 1117, "BaseUrl");

                var cmd = _database.CreateCommand("select ReferenceNo, PaidTime from ordertransactionfront where TransactionID=@tranId and ComputerID=@compId", conn);
                cmd.Parameters.Add(_database.CreateParameter("@tranId", payload.TransactionID));
                cmd.Parameters.Add(_database.CreateParameter("@compId", payload.ComputerID));
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        orderId = $"{reader.GetValue<string>("ReferenceNo")}{reader.GetValue<DateTime>("PaidTime").ToString("HHmmss")}";
                    }
                }
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(60);

                    if (!baseUrl.EndsWith("/"))
                        baseUrl = baseUrl + "/";

                    var grc = new GrcPaymentData();
                    try
                    {
                        var fintech = "";
                        if (payload.EDCType == 39)
                            fintech = "ovo";

                        var uri = new UriBuilder($"{baseUrl}check/{fintech}/{orderId}").ToString();
                        _log.Info($"Request GrcPaymentGateway for CheckPayment: {uri}");

                        var resp = await httpClient.GetAsync(uri);
                        if (resp.IsSuccessStatusCode)
                        {
                            var respContent = await resp.Content.ReadAsStringAsync();
                            grc = JsonConvert.DeserializeObject<GrcPaymentData>(respContent);

                            _log.Info($"Response from {uri}: {JsonConvert.SerializeObject(grc)}");

                            if (grc.response_code == "00")
                            {
                                var edcObject = new EdcObjLib.objCreditCardInfo()
                                {
                                    szTransactionDate = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                                    szTransactionTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                                    szCustAddress = $"{grc.store?.address1} {grc.store?.address2}",
                                    szBalanceAmount = grc.account?.cash_balance,
                                    szCustName = grc.account?.full_name,
                                    szRedeemPoint = grc.account?.ovo_points_used,
                                    szRedeemPointBalance = grc.account?.points_balance,
                                    szApprovalCode = "",
                                    szMerchantNo = "",
                                    szReferenceNo = "",
                                    szAmount = payload.PayAmount.ToString()
                                };
                                payload.ExtraParam = JsonConvert.SerializeObject(edcObject);

                                var tran = new TransactionPayload()
                                {
                                    ShopID = payload.ShopID,
                                    TransactionID = payload.TransactionID,
                                    ComputerID = payload.ComputerID,
                                    TerminalID = payload.ComputerID,
                                    StaffID = payload.StaffID,
                                    PrinterIds = payload.PrinterIds,
                                    PrinterNames = payload.PrinterNames
                                };

                                await _orderingService.SubmitOrderAsync(conn, payload.TransactionID, payload.ComputerID, payload.ShopID, payload.TableID);
                                await _printService.PrintOrder(tran);

                                await ConfirmKioskPaymentAsync(conn, payload);
                                result.Body = grc;
                            }
                            else
                            {
                                var errMsg = $"{grc.response_message}\n{grc.response_description}";
                                if (string.IsNullOrEmpty(grc.response_message))
                                    errMsg = "Unknow error!";

                                result.StatusCode = HttpStatusCode.InternalServerError;
                                result.Message = errMsg;

                                _log.Error($"{baseUrl} Fail: {errMsg}");
                            }
                        }
                        else
                        {
                            result.StatusCode = HttpStatusCode.InternalServerError;
                            result.Message = resp.ReasonPhrase;

                            _log.Error($"{baseUrl} Fail: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var message = ex.Message;
                        if (ex is TaskCanceledException)
                        {
                            message = $"Can't connect to {baseUrl} {ex.Message}";
                            result.StatusCode = HttpStatusCode.GatewayTimeout;
                            _log.Error(message);
                        }
                        result.Message = message;
                    }
                }
                else
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = "Not found GRC Payment Gateway base url (prop 1117)";

                    _log.Error("Not found GRC Payment Gateway base url (prop 1117)");
                }
            }
            return result;
        }

        async Task ConfirmKioskPaymentAsync(IDbConnection conn, PaymentData payload)
        {
            string saleDate = await _posRepo.GetSaleDateAsync(conn, payload.ShopID, true);

            string responseText = "";

            if (!string.IsNullOrEmpty(payload.TableName))
            {
                var cmd = _database.CreateCommand("update ordertransactionfront set QueueName=@tableName " +
                    " where TransactionID=@transactionId and ComputerID=@computerId", conn);
                cmd.Parameters.Add(_database.CreateParameter("@tableName", payload.TableName));
                cmd.Parameters.Add(_database.CreateParameter("@transactionId", payload.TransactionID));
                cmd.Parameters.Add(_database.CreateParameter("@computerId", payload.ComputerID));
                await _database.ExecuteNonQueryAsync(cmd);
            }

            if (payload.EDCType != 0)
            {
                var cmd = _database.CreateCommand("select PayTypeID from paytype where EDCType=@edcType", conn);
                cmd.Parameters.Add(_database.CreateParameter("@edcType", payload.EDCType));
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        payload.PayTypeID = reader.GetValue<int>("PayTypeID");
                    }
                }
                if (payload.PayTypeID == 0)
                    throw new VtecPOSException($"Not found PayType of EDCType {payload.EDCType}");
            }

            var dtPendingPayment = await _paymentService.GetPendingPaymentAsync(conn, payload.TransactionID, payload.ComputerID, payload.PayTypeID);
            if (dtPendingPayment.Rows.Count > 0)
                await _paymentService.DeletePaymentAsync(conn, dtPendingPayment.Rows[0].GetValue<int>("PayDetailID"), payload.TransactionID, payload.ComputerID);
            await _paymentService.AddPaymentAsync(conn, payload);

            var posModule = new POSModule();
            var isSuccess = posModule.Payment_Wallet(ref responseText, payload.WalletType, payload.ExtraParam, payload.TransactionID,
                payload.ComputerID, payload.PayDetailID.ToString(), payload.ShopID, saleDate, payload.BrandName,
                payload.WalletStoreId, payload.WalletDeviceId, conn as MySqlConnection);

            if (isSuccess)
            {
                await _paymentService.FinalizeBillAsync(conn, payload.TransactionID, payload.ComputerID, payload.TerminalID, payload.ShopID, payload.StaffID);
                await _paymentService.FinalizeOrderAsync(conn, payload.TransactionID, payload.ComputerID, payload.TerminalID,
                    payload.ShopID, payload.StaffID, payload.LangID, payload.PrinterIds, payload.PrinterNames);

                var printData = new PrintData()
                {
                    TransactionID = payload.TransactionID,
                    ComputerID = payload.ComputerID,
                    ShopID = payload.ShopID,
                    LangID = payload.LangID,
                    PrinterIds = payload.PrinterIds,
                    PrinterNames = payload.PrinterNames,
                    PaperSize = payload.PaperSize
                };

                await _printService.PrintBill(printData);
                _messenger.SendMessage();
            }
            else
            {
                throw new VtecPOSException(responseText);
            }
        }
    }
}