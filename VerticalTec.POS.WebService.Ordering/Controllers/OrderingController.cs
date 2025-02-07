﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using VerticalTec.POS.Database;
using VerticalTec.POS.OrderingApi.Services;
using VerticalTec.POS.Utils;
using VerticalTec.POS.WebService.Ordering.Models;
using VerticalTec.POS.WebService.Ordering.Services;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.WebService.Ordering.Controllers
{
    [ApiController]
    public class OrderingController : ControllerBase
    {
        IDatabase _database;
        IOrderingService _orderingService;
        ILogService _log;
        VtecPOSRepo _posRepo;

        public OrderingController(IDatabase database, IOrderingService orderingService, ILogService log)
        {
            _database = database;
            _orderingService = orderingService;
            _log = log;
            _posRepo = new VtecPOSRepo(database);
        }

        [HttpGet("v1/orders")]
        public async Task<IActionResult> GetOrdersDetailAsync(int transactionId, int computerId, int shopId, int langId)
        {
            var result = new CustomActionResult<List<OrderDetail>>();
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = await _orderingService.GetOrderDetailsAsync(conn, transactionId, computerId, shopId, langId: langId);
                }
                catch (VtecPOSException ex)
                {
                    _log.LogError(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpGet("v1/orders/summary")]
        public async Task<IActionResult> GetOrderSummaryAsync(int transactionId, int computerId, int shopId, int langId = 0)
        {
            var result = new CustomActionResult<object>(Request);
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
                    _log.LogError(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpPost("v1/orders/chsm")]
        public async Task<IActionResult> ChangeSaleMode(ChangeSaleModeOrder payload)
        {
            var result = new CustomActionResult<object>(Request);
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
                    _log.LogError(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        [HttpGet("v1/orders/billhtml")]
        public async Task<IActionResult> GetBillHtmlAsync(int transactionId, int computerId, int shopId, int langId = 0)
        {
            var result = new CustomActionResult<string>();
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = await _orderingService.GetBillHtmlAsync(conn, transactionId, computerId, shopId);
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        //TODO: v1/orders change to v1/orders/add
        [HttpPost("v1/orders/add")]
        public async Task<IActionResult> AddOrderAsync(OrderTransaction order)
        {
            _log.LogInfo($"ADD_ORDER {JsonConvert.SerializeObject(order)}");

            var response = new CustomActionResult<OrderTransaction>();
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
                    _log.LogError(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }
        //TODO: change v1/orders/update and http method
        [HttpPost("v1/orders/update")]
        public async Task<IActionResult> UpdateOrderAsync(OrderDetail orderDetail)
        {
            _log.LogInfo($"UPDATE_ORDER {JsonConvert.SerializeObject(orderDetail)}");
            var response = new CustomActionResult<OrderDetail>();
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
                    _log.LogError(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }
        //TODO: change v1/orders/combo/update http method
        [HttpPost("v1/orders/combo/update")]
        public async Task<IActionResult> UpdateComboOrderAsync(OrderTransaction orderTransaction)
        {
            var response = new CustomActionResult<OrderTransaction>();
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
                    _log.LogError(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }
        // TODO: change v1/orders/delete http method
        [HttpPost("v1/orders/delete")]
        public async Task<IActionResult> DeleteOrdersAsync(List<OrderDetail> orders)
        {
            _log.LogInfo($"DELETE_ORDER {JsonConvert.SerializeObject(orders)}");

            var response = new CustomActionResult<List<OrderDetail>>();
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
                    _log.LogError(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }
        //TODO: Change v1/orders/combo/update http POST and parameter to query
        [HttpPost("v2/orders/combo/update")]
        public async Task<IActionResult> EditComboOrdersAsync(int shopId, int transactionId, int computerId, int orderDetailId,
            List<OrderDetail> childOrders)
        {
            var response = new CustomActionResult<List<OrderDetail>>();
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

                    _log.LogInfo($"EDIT_COMBO {JsonConvert.SerializeObject(tranData)}");

                    await _orderingService.AddOrderAsync(conn, tranData);
                    var orderDetails = await _orderingService.GetOrderDetailsAsync(conn, transactionId, computerId, shopId);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Body = orderDetails;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message);

                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Message = ex.Message;
                }
            }
            return response;
        }
        //TODO: Change v1/orders/cancel http method to POST
        [HttpPost("v1/orders/cancel")]
        public async Task<IActionResult> CancelTransactionAsync(int transactionId, int computerId)
        {
            var result = new CustomActionResult<string>();
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await _orderingService.CancelTransactionAsync(conn, transactionId, computerId);
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = "";
                    MessengerService.Instance.SendMessage();
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = $"Cannot cancel transaction because {ex.Message}";
                }
            }
            return result;
        }

        [HttpGet("v1/orders/modifiers")]
        public async Task<IActionResult> GetModifierOrderAsync(int shopId, int transactionId = 0, int computerId = 0, int parentOrderDetailId = 0, string productCode = "")
        {
            var result = new CustomActionResult<object>(Request);
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
                    _log.LogError(ex.Message);

                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }
        //TODO: change v1/orders/move http method to POST
        [HttpPost("v1/orders/move")]
        public async Task<IActionResult> MoveOrderAsync(TableManage tableManage)
        {
            var result = new CustomActionResult<TableManage>();
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await _orderingService.MoveOrderAsync(conn, tableManage);
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = tableManage;

                    _log.LogInfo($"MOVE_ORDER {JsonConvert.SerializeObject(tableManage)}");
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }
        //TODO: v1/orders/submit http method to Post
        [HttpPost("v1/orders/submit")]
        public async Task<IActionResult> SubmitOrderAsync(Transaction transaction)
        {
            _log.LogInfo($"Submit order {JsonConvert.SerializeObject(transaction)}");
            var result = new CustomActionResult<string>();
            using (var conn = await _database.ConnectAsync())
            {
                await _orderingService.SubmitOrderAsync(conn, transaction);
            }
            MessengerService.Instance.SendMessage($"102|101|{transaction.TableID}");
            var parentId = BackgroundJob.Enqueue<PrintService>(p => p.PrintOrder(transaction));
            BackgroundJob.ContinueJobWith(parentId, () => MessengerService.Instance.SendMessage($"102|101|{transaction.TableID}"));
            return result;
        }
        //TODO: change v1/orders/checkbill to POST
        [HttpPost("v1/orders/checkbill")]
        public IActionResult CheckBillAsync(Transaction transaction)
        {
            var result = new CustomActionResult<string>();
            BackgroundJob.Enqueue<PrintService>(p => p.PrintCheckBill(transaction));
            result.StatusCode = HttpStatusCode.OK;
            result.Body = "";
            return result;
        }
        //TODO: v1/orders/salemode/submit to POST
        [HttpPost("v1/orders/salemode/submit")]
        public async Task<IActionResult> SubmitSaleModeOrderAsync(Transaction transaction)
        {
            _log.LogInfo($"Submit order {JsonConvert.SerializeObject(transaction)}");

            var result = new CustomActionResult<string>();

            using (var conn = await _database.ConnectAsync())
            {
                await _orderingService.SubmitSaleModeOrderAsync(conn, transaction);

                var shopType = await _posRepo.GetShopTypeAsync(conn, transaction.ShopID);
                if (shopType == ShopTypes.RestaurantTable)
                {
                    await _orderingService.SubmitOrderAsync(conn, transaction);
                    MessengerService.Instance.SendMessage($"102|101|{transaction.TableID}");
                    var parentId = BackgroundJob.Enqueue<PrintService>(p => p.PrintOrder(transaction));
                    BackgroundJob.ContinueJobWith(parentId, () => MessengerService.Instance.SendMessage($"102|101|{transaction.TableID}"));
                }
                else if (shopType == ShopTypes.FastFood)
                {
                    BackgroundJob.Enqueue<PrintService>(p => p.PrintCheckBill(transaction));
                }
            }
            result.StatusCode = HttpStatusCode.OK;
            result.Body = "";
            return result;
        }
        //TODO: v1/orders/kiosk/cancel to POST
        [HttpPost("v1/orders/kiosk/cancel")]
        public async Task<IActionResult> KioskCancelTransactionAsync(int transactionId, int computerId)
        {
            var result = new CustomActionResult<string>();
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
                    MessengerService.Instance.SendMessage();
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = $"Cannot cancel transaction because {ex.Message}";
            }
            return result;
        }
        //TODO: v1/orders/kiosk/printcheckbill POST
        [HttpPost("v1/orders/kiosk/printcheckbill")]
        public async Task<IActionResult> KioskPrintCheckBill(Transaction transaction)
        {
            var result = new CustomActionResult<string>();
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

                    BackgroundJob.Enqueue<PrintService>(p => p.PrintCheckBill(transaction));
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