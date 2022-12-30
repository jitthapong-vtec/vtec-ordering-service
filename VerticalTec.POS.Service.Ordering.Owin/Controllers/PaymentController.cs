using EdcObjLib;
using Hangfire;
using LoyaltyInterface.BlueCard;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO.Ports;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Exceptions;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Service.Ordering.Owin.Services;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;
using static LoyaltyInterface.BlueCard.BlueCardObj;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    public class PaymentController : ApiController
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetLogger("logpayment");

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

        [HttpPost]
        [Route("v1/payments/edc/kbank/qrinquiry")]
        public async Task<IHttpActionResult> InquiryKbankQRCodeAsync(PaymentData paymentData)
        {
            _logger.Info("Call v1/payments/edc/kbank/qrinquiry");

            var result = new HttpActionResult<object>(Request);
            var respText = "";
            try
            {
                var cardData = new objCreditCardInfo();
                var sPort = new SerialPort
                {
                    PortName = paymentData.EDCPort,
                    ReadTimeout = 60 * 1000 * 3,
                    WriteTimeout = 60 * 1000 * 3
                };

                var success = false;
                try
                {
                    success = EdcObjLib.KBank_OR_V4.ClassEdcLib_KBankOR_V4_PromptPay.SendEdc_PromptPayInquiry(sPort, "", "", ref cardData, ref respText);
                }
                catch (Exception ex)
                {
                    throw new ApiException(ErrorCodes.EDCComPort, ex.Message);
                }

                if (!success)
                {
                    _logger.Error($"Inquiry Error {respText}");
                    throw new ApiException(ErrorCodes.EDCInquiry, "");
                }

                using (var conn = await _database.ConnectAsync())
                {
                    string saleDate = await _posRepo.GetSaleDateAsync(conn, paymentData.ShopID, true);

                    //if (paymentData.EDCType != 0)
                    //{
                    //    var cmd = _database.CreateCommand("select PayTypeID from paytype where EDCType=@edcType", conn);
                    //    cmd.Parameters.Add(_database.CreateParameter("@edcType", paymentData.EDCType));
                    //    using (IDataReader reader = cmd.ExecuteReader())
                    //    {
                    //        if (reader.Read())
                    //        {
                    //            paymentData.PayTypeID = reader.GetValue<int>("PayTypeID");
                    //        }
                    //    }
                    //    if (paymentData.PayTypeID == 0)
                    //        throw new ApiException(ErrorCodes.NoPaymentConfig, $"Not found PayType of EDCType {paymentData.EDCType}");
                    //}

                    paymentData.PayTypeID = 100000300;

                    if (!string.IsNullOrEmpty(paymentData.MemberName))
                    {
                        var cmd = _database.CreateCommand("update ordertransactionfront set MemberName=@memberName where TransactionID=@tranId and ComputerID=@compId", conn);
                        cmd.Parameters.Add(_database.CreateParameter("@memberName", paymentData.MemberName));
                        cmd.Parameters.Add(_database.CreateParameter("@tranId", paymentData.TransactionID));
                        cmd.Parameters.Add(_database.CreateParameter("@compId", paymentData.ComputerID));
                        await _database.ExecuteNonQueryAsync(cmd);
                    }

                    var dtPendingPayment = await _paymentService.GetPendingPaymentAsync(conn, paymentData.TransactionID, paymentData.ComputerID, paymentData.PayTypeID);
                    if (dtPendingPayment.Rows.Count > 0)
                        await _paymentService.DeletePaymentAsync(conn, dtPendingPayment.Rows[0].GetValue<int>("PayDetailID"), paymentData.TransactionID, paymentData.ComputerID);
                    await _paymentService.AddPaymentAsync(conn, paymentData);

                    var posModule = new POSModule();
                    var cardDataJson = JsonConvert.SerializeObject(cardData);
                    success = posModule.Payment_Wallet(ref respText, paymentData.WalletType, cardDataJson, paymentData.TransactionID,
                        paymentData.ComputerID, paymentData.PayDetailID.ToString(), paymentData.ShopID, saleDate, paymentData.BrandName,
                        paymentData.WalletStoreId, paymentData.WalletDeviceId, conn as MySqlConnection);

                    if (!success)
                    {
                        throw new ApiException(ErrorCodes.PaymentFunction, respText);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(paymentData.MemberName))
                        {
                            _logger.Info("Call InsertTransPOS");

                            var blueCard = new BlueCard(paymentData.ShopID, paymentData.ComputerID, saleDate, conn as MySqlConnection);
                            var blueCardSuccess = blueCard.InsertTransPOS(ref respText, paymentData.ShopID, saleDate, paymentData.TransactionID, paymentData.ComputerID, "front", Card_No: "", paymentData.MemberName, 2, false, conn as MySqlConnection);
                            if (blueCardSuccess)
                            {
                                var systemData = new SystemData();
                                blueCardSuccess = blueCard.CommitTransPOS(ref respText, ref systemData, paymentData.TransactionID, paymentData.ComputerID, "front", conn as MySqlConnection);
                                if (blueCardSuccess)
                                    _logger.Info("InsertTransPOS success");
                                else
                                    _logger.Error("CommitTransPOS {0}", respText);
                            }
                            else
                            {
                                _logger.Error("InsertTransPOS {0}", respText);
                            }
                        }

                        await _orderingService.SubmitOrderAsync(conn, paymentData.TransactionID, paymentData.ComputerID, paymentData.ShopID, 0);

                        try
                        {
                            await _paymentService.FinalizeBillAsync(conn, paymentData.TransactionID, paymentData.ComputerID, paymentData.TerminalID, paymentData.ShopID, paymentData.StaffID);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error finalize");
                            _logger.Info("Retry finalize bill again");
                            try
                            {
                                await Task.Delay(500);
                                using (var conn2 = await _database.ConnectAsync())
                                {
                                    await _paymentService.FinalizeBillAsync(conn2, paymentData.TransactionID, paymentData.ComputerID, paymentData.TerminalID, paymentData.ShopID, paymentData.StaffID);
                                }
                            }
                            catch (Exception ex2)
                            {
                                _logger.Error(ex2, "Retry finalize bill failed");
                            }
                        }

                        var printData = new PrintData()
                        {
                            TransactionID = paymentData.TransactionID,
                            ComputerID = paymentData.ComputerID,
                            ShopID = paymentData.ShopID,
                            LangID = paymentData.LangID,
                            PrinterIds = paymentData.PrinterIds,
                            PrinterNames = paymentData.PrinterNames,
                            PaperSize = paymentData.PaperSize
                        };

                        await _printService.PrintBill(printData);
                        await _printService.PrintOrder(new TransactionPayload
                        {
                            TransactionID = paymentData.TransactionID,
                            ComputerID = paymentData.ComputerID,
                            TerminalID = paymentData.TerminalID,
                            ShopID = paymentData.ShopID,
                            LangID = paymentData.LangID,
                            StaffID = paymentData.StaffID,
                            PrinterIds = paymentData.PrinterIds,
                            PrinterNames = paymentData.PrinterNames
                        });
                        _messenger.SendMessage();
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                if (ex is ApiException apiEx)
                    result.ErrorCode = apiEx.ErrorCode;

                _logger.Error(ex.Message);
                result.StatusCode = HttpStatusCode.BadRequest;
            }
            return result;

        }
        [HttpPost]
        [Route("v1/payments/edc/kbank/genqr")]
        public IHttpActionResult GetKbankQRCode(string edcPort, decimal totalPrice)
        {
            _logger.Info("Call v1/payments/edc/kbank/genqr");

            var result = new HttpActionResult<string>(Request);
            var respText = "";
            try
            {
                var cardData = new objCreditCardInfo();
                var sPort = new SerialPort
                {
                    PortName = edcPort,
                    ReadTimeout = 60 * 1000 * 3,
                    WriteTimeout = 60 * 1000 * 3
                };

                var success = false;
                try
                {
                    success = EdcObjLib.KBank_OR_V4.ClassEdcLib_KBankOR_V4_PromptPay.SendEdc_PromptPayPayment(sPort, totalPrice, "", "", ref cardData, ref respText);
                }
                catch (Exception ex)
                {
                    throw new ApiException(ErrorCodes.EDCComPort, ex.Message);
                }

                if (!success)
                {
                    _logger.Error($"Edc GenQR Error {respText}");
                    throw new ApiException(ErrorCodes.EDCQRPayment, "");
                }
                result.Body = cardData.szQrPaymentInfo;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                if (ex is ApiException apiEx)
                    result.ErrorCode = apiEx.ErrorCode;

                _logger.Error(ex.Message);
                result.StatusCode = HttpStatusCode.BadRequest;
            }
            return result;
        }

        [HttpGet]
        [Route("v1/payments/edc/kbank/cancelqr")]
        public IHttpActionResult CancelKbankQRCode(string edcPort)
        {
            _logger.Info("Call v1/payments/edc/kbank/cancelqr");

            var result = new HttpActionResult<objCreditCardInfo>(Request);
            var respText = "";
            try
            {
                var cardData = new objCreditCardInfo();
                var sPort = new SerialPort
                {
                    PortName = edcPort,
                    ReadTimeout = 60 * 1000 * 3,
                    WriteTimeout = 60 * 1000 * 3
                };

                var success = false;
                try
                {
                    success = EdcObjLib.KBank_OR_V4.ClassEdcLib_KBankOR_V4_PromptPay.SendEdc_PromptPayCancel(sPort, "", "", ref cardData, ref respText);
                }
                catch (Exception ex)
                {
                    new ApiException(ErrorCodes.EDCComPort, ex.Message);
                }

                if (!success)
                {
                    _logger.Error($"Edc Cancel QR ERROR {respText}");
                    throw new ApiException(ErrorCodes.EDCCancelQR, "");
                }
                result.Body = cardData;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                if (ex is ApiException apiEx)
                    result.ErrorCode = apiEx.ErrorCode;

                result.StatusCode = HttpStatusCode.BadRequest;
            }
            return result;
        }

        [HttpPost]
        [Route("v1/payments/edc/kbank")]
        public async Task<IHttpActionResult> KbankEdcPayment(PaymentData paymentData)
        {
            _logger.Info("Call v1/payments/edc/kbank");

            var result = new HttpActionResult<object>(Request);
            try
            {
                var respText = "";
                using (var conn = await _database.ConnectAsync())
                {
                    var cardData = new objCreditCardInfo();
                    var sPort = new SerialPort
                    {
                        PortName = paymentData.EDCPort,
                        ReadTimeout = 60 * 1000 * 3,
                        WriteTimeout = 60 * 1000 * 3
                    };

                    string saleDate = await _posRepo.GetSaleDateAsync(conn, paymentData.ShopID, true);

                    //if (paymentData.EDCType != 0)
                    //{
                    //    var cmd = _database.CreateCommand("select PayTypeID from paytype where EDCType=@edcType", conn);
                    //    cmd.Parameters.Add(_database.CreateParameter("@edcType", paymentData.EDCType));
                    //    using (IDataReader reader = cmd.ExecuteReader())
                    //    {
                    //        if (reader.Read())
                    //        {
                    //            paymentData.PayTypeID = reader.GetValue<int>("PayTypeID");
                    //        }
                    //    }
                    //    if (paymentData.PayTypeID == 0)
                    //        throw new ApiException(ErrorCodes.NoPaymentConfig, $"Not found PayType of EDCType {paymentData.EDCType}");
                    //}

                    paymentData.PayTypeID = 102;

                    if (!string.IsNullOrEmpty(paymentData.MemberName))
                    {
                        var cmd = _database.CreateCommand("update ordertransactionfront set MemberName=@memberName where TransactionID=@tranId and ComputerID=@compId", conn);
                        cmd.Parameters.Add(_database.CreateParameter("@memberName", paymentData.MemberName));
                        cmd.Parameters.Add(_database.CreateParameter("@tranId", paymentData.TransactionID));
                        cmd.Parameters.Add(_database.CreateParameter("@compId", paymentData.ComputerID));
                        await _database.ExecuteNonQueryAsync(cmd);
                    }

                    var success = false;
#if DEBUG
                    success = true;
#else
                    try
                    {
                        if (paymentData.EDCType == 104)
                        {
                            success = EdcObjLib.KBank_OR_V4.ClassEdcLib_KBankOR_V4_Credit.SendEdc_CreditCardPayment(sPort, paymentData.PayAmount, "", "", ref cardData, ref respText);
                        }
                        else if (paymentData.EDCType == 111)
                        {
                            success = EdcObjLib.KBank_OR_V4.ClassEdcLib_KBankOR_V4_PromptPay.SendEdc_PromptPayInquiry(sPort, "", "", ref cardData, ref respText);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ApiException(ErrorCodes.EDCComPort, ex.Message);
                    }
#endif
                    if (!success)
                    {
                        var errCode = ErrorCodes.EDCCreditPayment;
                        var errMsg = $"Edc credit payment ERROR {respText}";

                        if (paymentData.EDCType == 111)
                        {
                            errCode = ErrorCodes.EDCQRPayment;
                            errMsg = $"Edc qr payment ERROR {respText}";
                        }

                        _logger.Error(errMsg);
                        throw new ApiException(errCode, "");
                    }

                    var dtPendingPayment = await _paymentService.GetPendingPaymentAsync(conn, paymentData.TransactionID, paymentData.ComputerID, paymentData.PayTypeID);
                    if (dtPendingPayment.Rows.Count > 0)
                        await _paymentService.DeletePaymentAsync(conn, dtPendingPayment.Rows[0].GetValue<int>("PayDetailID"), paymentData.TransactionID, paymentData.ComputerID);
                    await _paymentService.AddPaymentAsync(conn, paymentData);

                    var posModule = new POSModule();
                    var cardDataJson = JsonConvert.SerializeObject(cardData);
#if DEBUG == false
                    success = posModule.Payment_Wallet(ref respText, paymentData.WalletType, cardDataJson, paymentData.TransactionID,
                        paymentData.ComputerID, paymentData.PayDetailID.ToString(), paymentData.ShopID, saleDate, paymentData.BrandName,
                        paymentData.WalletStoreId, paymentData.WalletDeviceId, conn as MySqlConnection);
#endif
                    if (!success)
                    {
                        throw new ApiException(ErrorCodes.PaymentFunction, respText);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(paymentData.MemberName))
                        {
                            _logger.Info("Call InsertTransPOS");

                            var blueCard = new BlueCard(paymentData.ShopID, paymentData.ComputerID, saleDate, conn as MySqlConnection);
                            var blueCardSuccess = blueCard.InsertTransPOS(ref respText, paymentData.ShopID, saleDate, paymentData.TransactionID, paymentData.ComputerID, "front", Card_No: "", paymentData.MemberName, 2, false, conn as MySqlConnection);
                            if (blueCardSuccess)
                            {
                                var systemData = new SystemData();
                                blueCardSuccess = blueCard.CommitTransPOS(ref respText, ref systemData, paymentData.TransactionID, paymentData.ComputerID, "front", conn as MySqlConnection);
                                if (blueCardSuccess)
                                    _logger.Info("InsertTransPOS success");
                                else
                                    _logger.Error("CommitTransPOS {0}", respText);
                            }
                            else
                            {
                                _logger.Error("InsertTransPOS {0}", respText);
                            }
                        }

                        await _orderingService.SubmitOrderAsync(conn, paymentData.TransactionID, paymentData.ComputerID, paymentData.ShopID, 0);
                        try
                        {
                            await _paymentService.FinalizeBillAsync(conn, paymentData.TransactionID, paymentData.ComputerID, paymentData.TerminalID, paymentData.ShopID, paymentData.StaffID);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error finalize");
                            _logger.Info("Retry finalize bill again");
                            try
                            {
                                await Task.Delay(500);
                                using (var conn2 = await _database.ConnectAsync())
                                {
                                    await _paymentService.FinalizeBillAsync(conn2, paymentData.TransactionID, paymentData.ComputerID, paymentData.TerminalID, paymentData.ShopID, paymentData.StaffID);
                                }
                            }
                            catch (Exception ex2)
                            {
                                _logger.Error(ex2, "Retry finalize bill failed");
                            }
                        }
                        var printData = new PrintData()
                        {
                            TransactionID = paymentData.TransactionID,
                            ComputerID = paymentData.ComputerID,
                            ShopID = paymentData.ShopID,
                            LangID = paymentData.LangID,
                            PrinterIds = paymentData.PrinterIds,
                            PrinterNames = paymentData.PrinterNames,
                            PaperSize = paymentData.PaperSize
                        };
                        await _printService.PrintBill(printData);
                        await _printService.PrintOrder(new TransactionPayload
                        {
                            TransactionID = paymentData.TransactionID,
                            ComputerID = paymentData.ComputerID,
                            TerminalID = paymentData.TerminalID,
                            ShopID = paymentData.ShopID,
                            LangID = paymentData.LangID,
                            StaffID = paymentData.StaffID,
                            PrinterIds = paymentData.PrinterIds,
                            PrinterNames = paymentData.PrinterNames
                        });
                        _messenger.SendMessage();
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                if (ex is ApiException apiEx)
                    result.ErrorCode = apiEx.ErrorCode;

                _logger.Error(ex.Message);
                result.StatusCode = HttpStatusCode.BadRequest;
            }
            return result;
        }

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
            _logger.Info($"AddPayment => {JsonConvert.SerializeObject(paymentData)}");

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
            _logger.Info($"FinalizeBill => {JsonConvert.SerializeObject(payload)}");

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
                        _logger.Info($"Request GrcPaymentGateway: {JsonConvert.SerializeObject(grcPayload)}");

                        var uri = new UriBuilder($"{baseUrl}pay").ToString();
                        var json = JsonConvert.SerializeObject(grcPayload);
                        var reqContent = new StringContent(json);
                        reqContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        var resp = await httpClient.PostAsync(uri, reqContent);
                        if (resp.IsSuccessStatusCode)
                        {
                            var respContent = await resp.Content.ReadAsStringAsync();
                            grc = JsonConvert.DeserializeObject<GrcPaymentData>(respContent);

                            _logger.Info($"Response from {uri}: {JsonConvert.SerializeObject(grc)}");

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
                                _logger.Error($"{baseUrl} Fail: {result.Message}");
                            }
                        }
                        else
                        {
                            result.StatusCode = HttpStatusCode.InternalServerError;
                            result.Message = resp.ReasonPhrase;
                            _logger.Error($"{baseUrl} Fail: {result.Message}");
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
                        _logger.Error(message);
                    }
                }
                else
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = "Not found GRC Payment Gateway base url (prop 1117)";

                    _logger.Error("Not found GRC Payment Gateway base url (prop 1117)");
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
                        _logger.Info($"Request GrcPaymentGateway for CheckPayment: {uri}");

                        var resp = await httpClient.GetAsync(uri);
                        if (resp.IsSuccessStatusCode)
                        {
                            var respContent = await resp.Content.ReadAsStringAsync();
                            grc = JsonConvert.DeserializeObject<GrcPaymentData>(respContent);

                            _logger.Info($"Response from {uri}: {JsonConvert.SerializeObject(grc)}");

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

                                _logger.Error($"{baseUrl} Fail: {errMsg}");
                            }
                        }
                        else
                        {
                            result.StatusCode = HttpStatusCode.InternalServerError;
                            result.Message = resp.ReasonPhrase;

                            _logger.Error($"{baseUrl} Fail: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var message = ex.Message;
                        if (ex is TaskCanceledException)
                        {
                            message = $"Can't connect to {baseUrl} {ex.Message}";
                            result.StatusCode = HttpStatusCode.GatewayTimeout;
                            _logger.Error(message);
                        }
                        result.Message = message;
                    }
                }
                else
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = "Not found GRC Payment Gateway base url (prop 1117)";

                    _logger.Error("Not found GRC Payment Gateway base url (prop 1117)");
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