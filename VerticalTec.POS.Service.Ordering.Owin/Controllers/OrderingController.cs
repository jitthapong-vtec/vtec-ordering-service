﻿using Hangfire;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Service.Ordering.Owin.Services;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    public class OrderingController : ApiController
    {
        public static readonly object Owner = new object();

        static readonly NLog.Logger _logger = NLog.LogManager.GetLogger("logordering");

        IDatabase _database;
        IOrderingService _orderingService;
        IMessengerService _messengerService;
        IPrintService _printService;
        VtecPOSRepo _posRepo;

        public OrderingController(IDatabase database, IOrderingService orderingService, IMessengerService messenger, IPrintService printService)
        {
            _database = database;
            _orderingService = orderingService;
            _messengerService = messenger;
            _printService = printService;
            _posRepo = new VtecPOSRepo(database);
        }

        [HttpPost]
        [Route("v1/orders/online")]
        public IHttpActionResult OnlineOrder(POSObject.OrderObj payload)
        {
            lock (Owner)
            {
                var result = new SimpleHttpActionResult(Request);

                try
                {
                    var clientId = Request.Headers.GetValues("x-client-id").FirstOrDefault();
                    var clientSecret = Request.Headers.GetValues("x-client-secret").FirstOrDefault();

                    if (clientId != "vtec-ordering" && clientSecret != "688635a6c68f85e8")
                    {
                        result.StatusCode = HttpStatusCode.Unauthorized;
                        result.Message = "Unauthorized";
                        return result;
                    }
                }
                catch
                {
                    result.StatusCode = HttpStatusCode.Unauthorized;
                    result.Message = "Unauthorized";
                    return result;
                }

                try
                {
                    var posModule = new POSModule();
                    var responseMsg = "";
                    var transactionId = 0;
                    var computerId = 0;
                    var staffId = 2;
                    var tranKey = "";
                    var isPrint = false;
                    var jsonData = JsonConvert.SerializeObject(payload);

                    using (var conn = _database.Connect())
                    {
                        var dtShop = _posRepo.GetShopDataAsync(conn).Result;
                        var shopId = dtShop.AsEnumerable().FirstOrDefault()?.GetValue<int>("ShopID") ?? 0;
                        if (shopId == 0)
                        {
                            result.StatusCode = HttpStatusCode.BadRequest;
                            result.Message = "Not found shop";
                            return result;
                        }

                        var saleDate = "";
                        try
                        {
                            saleDate = _posRepo.GetSaleDateAsync(conn, shopId, false).Result;
                        }
                        catch
                        {
                            result.StatusCode = HttpStatusCode.BadRequest;
                            result.Message = "Store is not open";
                            return result;
                        }

                        var sessionId = 0;
                        var terminalId = 0;
                        var cmd = _database.CreateCommand("select SessionID, ComputerID from session where CloseStaffId=0 order by SessionDate desc limit 1", conn);
                        using (var reader = _database.ExecuteReaderAsync(cmd).Result)
                        {
                            if (reader.Read())
                            {
                                sessionId = reader.GetValue<int>("SessionID");
                                terminalId = reader.GetValue<int>("ComputerID");
                            }
                        }
                        if (sessionId == 0)
                        {
                            result.StatusCode = HttpStatusCode.BadRequest;
                            result.Message = "There is no open session";
                            return result;
                        }

                        var decimalDigit = _posRepo.GetDefaultDecimalDigitAsync(conn).Result;

                        var success = posModule.OrderAPI_VTEC(ref responseMsg, ref transactionId, ref computerId, ref tranKey, ref isPrint, jsonData, shopId, $"'{saleDate}'", sessionId, terminalId, staffId, decimalDigit, conn as MySqlConnection);

                        if (success)
                        {
                            if (isPrint)
                            {
                                var tableId = 0;
                                cmd.CommandText = "select TableID from order_tablefront where TransactionID=@transId and ComputerID=@compId and SaleDate=@saleDate and ShopID=@shopId";
                                cmd.Parameters.Add(_database.CreateParameter("@transId", transactionId));
                                cmd.Parameters.Add(_database.CreateParameter("@compId", computerId));
                                cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
                                cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));

                                using (var reader = _database.ExecuteReaderAsync(cmd).Result)
                                {
                                    if (reader.Read())
                                    {
                                        tableId = reader.GetValue<int>("TableID");
                                    }
                                }

                                var transPayload = new TransactionPayload()
                                {
                                    TransactionID = transactionId,
                                    ComputerID = computerId,
                                    ShopID = shopId,
                                    TerminalID = terminalId,
                                    TableID = tableId,
                                    StaffID = staffId
                                };

                                try
                                {
                                    _orderingService.SubmitOrderAsync(conn, transactionId, computerId, shopId, tableId).ConfigureAwait(false);
                                    _printService.PrintOrder(transPayload).ConfigureAwait(false);
                                    _messengerService.SendMessage($"102|101|{transPayload.TableID}");
                                }
                                catch (Exception ex)
                                {
                                    result.StatusCode = HttpStatusCode.BadRequest;
                                    result.Message = ex.Message;
                                    return result;
                                }
                            }
                        }
                        else
                        {
                            result.StatusCode = HttpStatusCode.BadRequest;
                            result.Message = responseMsg;
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.StatusCode = HttpStatusCode.BadRequest;
                    result.Message = ex.Message;
                }
                return result;
            }
        }

        [HttpGet]
        [Route("v1/orders")]
        public async Task<IHttpActionResult> GetOrdersDetailAsync(int transactionId, int computerId, int shopId, int langId = 1)
        {
            var result = new HttpActionResult<List<OrderDetail>>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var orders = await _orderingService.GetOrderDetailsAsync(conn, transactionId, computerId, shopId, langId: langId);
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = orders;
                }
                catch (VtecPOSException ex)
                {
                    _logger.Error(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/orders/summary")]
        public async Task<IHttpActionResult> GetOrderSummaryAsync(int transactionId, int computerId, int shopId, int langId = 1)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var dataSet = await _orderingService.GetOrderDataAsync(conn, transactionId, computerId, shopId, langId);
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = dataSet;
                }
                catch (VtecPOSException ex)
                {
                    _logger.Error(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/orders/smcharge")]
        public async Task<IHttpActionResult> UpdateOrderSaleModeCharge(int shopId, int transactionId, int computerId, int saleMode)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var posModule = new POSModule();
                var responseText = "";
                var saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, true);
                var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                var success = posModule.OrderDetail_SaleModeCharge(ref responseText, transactionId, computerId, saleDate,
                    shopId, saleMode, decimalDigit, "front", conn as MySqlConnection);

                if (!success)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = responseText;
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/orders/chsm")]
        public async Task<IHttpActionResult> ChangeSaleMode(ChangeSaleModeOrder payload)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var myConn = conn as MySqlConnection;
                try
                {
                    var responseText = "";
                    var posModule = new POSModule();
                    var saleDate = await _posRepo.GetSaleDateAsync(conn, payload.ShopID, true);
                    var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                    var success = posModule.OrderDetail_ChangeSaleMode(ref responseText, (int)payload.SaleMode, payload.OrderDetailID,
                         payload.TransactionID, payload.ComputerID, payload.ShopID, saleDate, "front", decimalDigit, myConn);
                    if (success)
                    {
                        posModule.OrderDetail_RefreshPromo(ref responseText, "front", payload.TransactionID, payload.ComputerID, decimalDigit, myConn);
                        posModule.OrderDetail_CalBill(payload.TransactionID, payload.ComputerID, payload.ShopID, decimalDigit, "front", myConn);
                        result.StatusCode = HttpStatusCode.OK;
                        result.Body = "";
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = responseText;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/orders/billhtml")]
        public async Task<IHttpActionResult> GetBillHtmlAsync(int transactionId, int computerId, int shopId, int langId = 0)
        {
            var result = new HttpActionResult<string>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var billHtml = await _orderingService.GetBillHtmlAsync(conn, transactionId, computerId, shopId);
                if (!string.IsNullOrEmpty(billHtml))
                {
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = billHtml;
                }
                else
                {
                    result.StatusCode = HttpStatusCode.NotFound;
                    result.Message = "Not found bill html";
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/orders")]
        public async Task<IHttpActionResult> AddOrderAsync(OrderTransaction order)
        {
            _logger.Info($"ADD_ORDER {JsonConvert.SerializeObject(order)}");

            var response = new HttpActionResult<OrderTransaction>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await _orderingService.AddOrderAsync(conn, order);
                    //TODO: AddAutoProductSaleMode
                    //if (order.SaleMode != SaleModes.DineIn)
                    //{
                    //    try
                    //    {
                    //        var smProId = _orderingService.AutoAddProductBySaleMode(conn, order.TransactionID, order.ComputerID, order.ShopID, order.SaleMode);
                    //        order.Orders = await _orderingService.GetOrderDetailsAsync(conn, order.TransactionID,
                    //            order.ComputerID, order.ShopID, order.StaffID, order.LangID);
                    //        if (!string.IsNullOrEmpty(smProId))
                    //        {
                    //            int proId;
                    //            if (int.TryParse(smProId, out proId))
                    //            {
                    //                var query = order.Orders.Where(smOrder => smOrder.ProductID == proId).ToList();
                    //                foreach (var item in query)
                    //                {
                    //                    item.EnableDelete = false;
                    //                    item.EnableModifyQty = false;
                    //                }
                    //            }
                    //        }
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        LogService.Instance.WriteLog(WebApiApplication.LogPrefix, $"ADD_ORDER:AUTO_ADD_SALEMODE {ex.Message}");
                    //    }
                    //}
                    response.StatusCode = HttpStatusCode.OK;
                    response.Body = order;
                }
                catch (VtecPOSException ex)
                {
                    _logger.Error(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }

        [HttpPost]
        [Route("v1/orders/update")]
        public async Task<IHttpActionResult> UpdateOrderAsync(OrderDetail orderDetail)
        {
            _logger.Info($"UPDATE_ORDER {JsonConvert.SerializeObject(orderDetail)}");

            var response = new HttpActionResult<OrderDetail>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await _orderingService.ModifyOrderAsync(conn, orderDetail);
                    var dtStock = await _posRepo.GetCurrentStockAsync(conn, orderDetail.ProductID, orderDetail.ShopID);
                    if (dtStock.Rows.Count > 0)
                    {
                        orderDetail.EnableCountDownStock = true;
                        orderDetail.CurrentStock = dtStock.Rows[0].GetValue<double>("CurrentStock");
                    }
                    else
                    {
                        orderDetail.EnableCountDownStock = false;
                        orderDetail.CurrentStock = 1;
                    }
                    response.StatusCode = HttpStatusCode.OK;
                    response.Body = orderDetail;
                }
                catch (VtecPOSException ex)
                {
                    _logger.Error(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }

        [HttpPost]
        [Route("v1/orders/combo/update")]
        public async Task<IHttpActionResult> UpdateComboOrderAsync(OrderTransaction orderTransaction)
        {
            var response = new HttpActionResult<OrderTransaction>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await _orderingService.DeleteOrdersAsync(conn, orderTransaction.Orders);
                    await _orderingService.AddOrderAsync(conn, orderTransaction);

                    response.StatusCode = HttpStatusCode.OK;
                    response.Body = orderTransaction;
                }
                catch (VtecPOSException ex)
                {
                    _logger.Error(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }

        [HttpPost]
        [Route("v1/orders/delete")]
        public async Task<IHttpActionResult> DeleteOrdersAsync(List<OrderDetail> orders)
        {
            _logger.Info($"DELETE_ORDER {JsonConvert.SerializeObject(orders)}");

            var response = new HttpActionResult<List<OrderDetail>>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var orderDetails = await _orderingService.DeleteOrdersAsync(conn, orders);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Body = orderDetails;
                }
                catch (VtecPOSException ex)
                {
                    _logger.Error(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }

        [HttpPost]
        [Route("v2/orders/combo/update")]
        public async Task<IHttpActionResult> EditComboOrdersAsync(int shopId, int transactionId, int computerId, int orderDetailId,
            List<OrderDetail> childOrders)
        {
            var response = new HttpActionResult<List<OrderDetail>>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var dtChildOrder = await _orderingService.GetChildOrderAsync(conn, transactionId, computerId, orderDetailId);
                    var dtCurrentStock = await _posRepo.GetCurrentStockAsync(conn, 0, shopId);
                    var orderHaveStock = (from childOrder in dtChildOrder.AsEnumerable()
                                          join stock in dtCurrentStock.AsEnumerable()
                                          on childOrder.GetValue<int>("ProductID") equals stock.GetValue<int>("ProductID")
                                          select new
                                          {
                                              ProductID = childOrder.GetValue<int>("ProductID"),
                                              TotalQty = childOrder.GetValue<double>("TotalQty")
                                          });
                    var totalStock = orderHaveStock.GroupBy(p => p.ProductID).Select(p => new { ProductID = p.First().ProductID, TotalQty = p.Sum(o => o.TotalQty) });
                    await _orderingService.DeleteChildComboAsync(conn, transactionId, computerId, orderDetailId);
                    foreach (var stock in totalStock)
                    {
                        var cmd = _database.CreateCommand(conn);
                        cmd.CommandText = "update productcountdownstock set CurrentStock=CurrentStock+@stock, UpdateDate=Now() where ProductID=@productId and ShopID=@shopId";
                        cmd.Parameters.Add(_database.CreateParameter("@stock", stock.TotalQty));
                        cmd.Parameters.Add(_database.CreateParameter("@productId", stock.ProductID));
                        cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                        await _database.ExecuteNonQueryAsync(cmd);
                    }

                    foreach (var childOrder in childOrders.Where(o => !new int[] { 14, 15 }.Contains(o.ProductTypeID)))
                    {
                        childOrder.OrderDetailLinkID = orderDetailId;
                    }
                    var tranData = new OrderTransaction()
                    {
                        TransactionID = transactionId,
                        ComputerID = computerId,
                        ShopID = shopId,
                        Orders = childOrders
                    };

                    _logger.Info($"EDIT_COMBO {JsonConvert.SerializeObject(tranData)}");

                    await _orderingService.AddOrderAsync(conn, tranData);
                    var orderDetails = await _orderingService.GetOrderDetailsAsync(conn, transactionId, computerId, shopId);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Body = orderDetails;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }

        [HttpPost]
        [Route("v1/orders/cancel")]
        public async Task<IHttpActionResult> CancelTransactionAsync(int transactionId, int computerId)
        {
            var result = new HttpActionResult<string>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await _orderingService.CancelTransactionAsync(conn, transactionId, computerId);
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = "";
                    _messengerService.SendMessage();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = $"Cannot cancel transaction because {ex.Message}";
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/orders/modifiers")]
        public async Task<IHttpActionResult> GetModifierOrderAsync(int shopId, int transactionId = 0, int computerId = 0, int parentOrderDetailId = 0, string productCode = "")
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var modifierOrder = await _orderingService.GetModifierOrderAsync(conn, shopId, transactionId, computerId, parentOrderDetailId, productCode);

                    var cmd = _database.CreateCommand("select * from productdept a" +
                        " inner join productgroup b" +
                        " on a.ProductGroupID = b.ProductGroupID" +
                        " where b.IsComment = 1 " +
                        " and a.Deleted = 0 " +
                        " and a.ProductDeptActivate = 1", conn);

                    var commentDeptIds = modifierOrder.AsEnumerable().GroupBy(m => m.GetValue<int>("ProductDeptID")).Select(m => m.Key).ToList();
                    if (commentDeptIds.Count > 0)
                    {
                        var deptIds = "";
                        for (var i = 0; i < commentDeptIds.Count; i++)
                        {
                            deptIds += commentDeptIds[i];
                            if (i < commentDeptIds.Count - 1)
                                deptIds += ",";
                        }
                        cmd.CommandText += " and a.ProductDeptID in (" + deptIds + ")";
                    }

                    DataTable dtModifierDept = new DataTable();
                    using (var reader = await _database.ExecuteReaderAsync(cmd))
                    {
                        dtModifierDept.Load(reader);
                    }
                    var modifierData = new
                    {
                        ModifierDepts = (from dept in dtModifierDept.AsEnumerable()
                                         select new
                                         {
                                             ProductDeptID = dept.GetValue<int>("ProductDeptID"),
                                             ProductDeptName = dept.GetValue<string>("ProductDeptName"),
                                             DisplayMobile = dept.GetValue<int>("DisplayMobile")
                                         }),
                        Modifiers = modifierOrder
                    };
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = modifierData;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/orders/move")]
        public async Task<IHttpActionResult> MoveOrderAsync(TableManage tableManage)
        {
            var result = new HttpActionResult<TableManage>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var dsPrintData = await _orderingService.MoveOrderAsync(conn, tableManage);

                    await _printService.PrintAsync(tableManage.ShopID, tableManage.ComputerID, dsPrintData);
                    _messengerService.SendMessage();

                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = tableManage;

                    _logger.Info($"MOVE_ORDER {JsonConvert.SerializeObject(tableManage)}");
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;

                    _logger.Info($"MOVE_ORDER {ex.Message}");
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/orders/submit")]
        public async Task<IHttpActionResult> SubmitOrderAsync(TransactionPayload transaction)
        {
            _logger.Info($"Submit order {JsonConvert.SerializeObject(transaction)}");

            var result = new HttpActionResult<string>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                await _orderingService.SubmitOrderAsync(conn, transaction.TransactionID, transaction.ComputerID, transaction.ShopID, transaction.TableID);

                var jobId = BackgroundJob.Enqueue(() => _printService.PrintOrder(transaction));
                BackgroundJob.ContinueJobWith(jobId, () => _messengerService.SendMessage($"102|101|{transaction.TableID}"));
            }
            return result;
        }

        [HttpPost]
        [Route("v1/orders/checkbill")]
        public async Task<IHttpActionResult> CheckBillAsync(TransactionPayload payload)
        {
            _logger.Info($"Check bill {JsonConvert.SerializeObject(payload)}");

            var result = new HttpActionResult<string>(Request);
            await _printService.PrintCheckBill(payload);

            result.StatusCode = HttpStatusCode.OK;
            result.Body = "";
            return result;
        }

        [HttpPost]
        [Route("v1/orders/salemode/submit")]
        public async Task<IHttpActionResult> SubmitSaleModeOrderAsync(TransactionPayload payload)
        {
            _logger.Info($"Submit order {JsonConvert.SerializeObject(payload)}");

            var result = new HttpActionResult<string>(Request);

            using (var conn = await _database.ConnectAsync())
            {
                await _orderingService.SubmitSaleModeOrderAsync(conn, payload.TransactionID, payload.ComputerID,
                    payload.TransactionName, payload.TotalCustomer, payload.TransactionStatus);

                var shopType = await _posRepo.GetShopTypeAsync(conn, payload.ShopID);
                if (shopType == ShopTypes.RestaurantTable)
                {
                    await _orderingService.SubmitOrderAsync(conn, payload.TransactionID, payload.ComputerID, payload.ShopID, payload.TableID);
                }
                else if (shopType == ShopTypes.FastFood)
                {
                    await _printService.PrintCheckBill(payload);
                }
                await _printService.PrintOrder(payload);
                _messengerService.SendMessage($"102|101|{payload.TableID}");
            }
            result.StatusCode = HttpStatusCode.OK;
            result.Body = "";
            return result;
        }

        [HttpPost]
        [Route("v1/orders/kiosk/cancel")]
        public async Task<IHttpActionResult> KioskCancelTransactionAsync(int transactionId, int computerId)
        {
            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    await Task.Run(() =>
                    {
                        string sqlUpdate = "update ordertransactionfront " +
                                    " set TransactionStatusID=@status" +
                                    " where TransactionID=@transactionId" +
                                    " and ComputerID=@computerId";
                        var cmd = _database.CreateCommand(conn);
                        cmd.CommandText = sqlUpdate;
                        cmd.Parameters.Add(_database.CreateParameter("@status", 99));
                        cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
                        cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
                        cmd.ExecuteNonQuery();
                    });
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = "";
                    _messengerService.SendMessage();
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = $"Cannot cancel transaction because {ex.Message}";
            }
            return result;
        }

        [HttpPost]
        [Route("v1/orders/kiosk/printcheckbill")]
        public async Task<IHttpActionResult> KioskPrintCheckBill(TransactionPayload transaction)
        {
            _logger.Info($"Check bill {JsonConvert.SerializeObject(transaction)}");

            var result = new HttpActionResult<string>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    if (!string.IsNullOrEmpty(transaction.TableName))
                    {
                        var cmd = _database.CreateCommand("update ordertransactionfront set TableName=@tableName," +
                            " TransactionStatusID=@status" +
                            " where TransactionID=@transactionId and ComputerID=@computerId", conn);
                        cmd.Parameters.Add(_database.CreateParameter("@tableName", transaction.TableName));
                        cmd.Parameters.Add(_database.CreateParameter("@transactionId", transaction.TransactionID));
                        cmd.Parameters.Add(_database.CreateParameter("@status", transaction.TransactionStatus));
                        cmd.Parameters.Add(_database.CreateParameter("@computerId", transaction.TerminalID));
                        cmd.ExecuteNonQuery();
                    }

                    await _printService.KioskPrintCheckBill(transaction);
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }
    }
}
