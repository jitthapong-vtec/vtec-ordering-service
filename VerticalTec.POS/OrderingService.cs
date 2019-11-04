using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using vtecPOS.GlobalFunctions;
using vtecPOS.POSControl;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS
{
    public class OrderingService : IOrderingService
    {
        IDatabase _database;
        VtecPOSRepo _posRepo;
        POSModule _posModule;

        public OrderingService(IDatabase database)
        {
            _database = database;
            _posModule = new POSModule();
            _posRepo = new VtecPOSRepo(database);
        }

        public async Task AddOrderAsync(IDbConnection conn, OrderTransaction orderData)
        {
            await OpenTransactionAsync(conn, orderData);

            string resultText = string.Empty;
            var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
            var saleDate = await _posRepo.GetSaleDateAsync(conn, orderData.ShopID, true);

            foreach (var order in orderData.Orders)
            {
                if (order.ParentProductID > 0 && order.OrderDetailLinkID == 0)
                {
                    order.OrderDetailLinkID = orderData.Orders.Where(o => order.ParentProductID == o.ProductID).Select(o => o.OrderDetailID).FirstOrDefault();
                }

                int OrderDetailId = 0;
                order.TransactionID = orderData.TransactionID;
                order.ComputerID = orderData.ComputerID;
                order.SaleDate = saleDate;
                order.OrderComputerID = orderData.TerminalID;
                order.OrderTableID = orderData.TableID;
                order.ShopID = orderData.ShopID;
                order.DecimalDigit = decimalDigit;
                if (order.OrderStaffID == 0)
                    order.OrderStaffID = orderData.StaffID;

                bool isSuccess = _posModule.OrderDetail(ref resultText, ref OrderDetailId, false, (int)order.SaleMode,
                    order.TransactionID, order.ComputerID, order.OrderDetailLinkID, order.IndentLevel, order.ProductID,
                    order.TotalQty, order.OpenPrice, "front", decimalDigit, saleDate,
                    order.ShopID, order.IsComponentProduct, order.ParentProductID, order.PGroupID, order.OtherFoodName,
                    order.OtherProductGroupID, order.OtherPrinterID, order.OtherDiscountAllow, order.OtherInventoryID,
                    order.OtherVatType, order.OtherPrintGroup, order.OtherProductVatCode, order.OtherHasSc,
                    order.OtherProductTypeID, order.OrderStaffID, order.OrderComputerID, order.OrderTableID, order.SetGroupNo,
                    order.QtyRatio, conn as MySqlConnection);
                if (isSuccess)
                    order.OrderDetailID = OrderDetailId;
                else
                    throw new VtecPOSException(resultText);
            }
            orderData.Orders = await GetOrderDetailsAsync(conn, orderData.TransactionID, orderData.ComputerID,
                orderData.ShopID, orderData.StaffID, orderData.LangID);


        }

        public Task<bool> CancelTransactionAsync(IDbConnection conn, int transactionId, int computerId)
        {
            throw new NotImplementedException();
        }

        public async Task<DataSet> CheckBillAsync(IDbConnection conn, Transaction transaction)
        {
            var myConn = conn as MySqlConnection;
            var responseText = "";
            var unSubmitOrder = 0;
            var saleDate = await _posRepo.GetSaleDateAsync(conn, transaction.ShopID, true);

            _posModule.Table_CheckSubmitOrder(ref responseText, "front", ref unSubmitOrder, transaction.TransactionID,
                transaction.ComputerID, transaction.ShopID, saleDate, transaction.LangID, myConn);
            if (unSubmitOrder > 0)
            {
                throw new VtecPOSException(string.IsNullOrEmpty(responseText) ? "Some orders are not submit to kitchen please submit orders first" : responseText);
            }

            var receiptHtml = "";
            var receiptCopyHtml = "";
            var noPrintCopy = 0;
            DataSet dsPrintData = new DataSet();
            var isSuccess = _posModule.BillCheck(ref responseText, ref receiptHtml, ref receiptCopyHtml, ref noPrintCopy, ref dsPrintData,
                (int)ViewBillTypes.Print, transaction.TransactionID, transaction.ComputerID, transaction.ShopID, 0,
                "front", transaction.LangID, transaction.StaffID, transaction.TerminalID, 1, myConn);
            if (!isSuccess)
                throw new VtecPOSException(responseText);
            return dsPrintData;
        }

        public async Task<List<OrderDetail>> DeleteOrdersAsync(IDbConnection conn, List<OrderDetail> orders)
        {
            foreach (var order in orders)
            {
                await EditOrderAsync(conn, order, OrdersModifyTypes.Delete);
            }
            return await GetOrderDetailsAsync(conn, orders[0].TransactionID, orders[0].ComputerID, orders[0].ShopID, orders[0].OrderStaffID);
        }

        public async Task<string> GetBillHtmlAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int langId = 1)
        {
            var myConn = conn as MySqlConnection;

            string receiptHtml = "";
            int noCopy = 0;
            string copyReceiptString = "";
            string responseText = "";
            var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);

            _posModule.OrderDetail_RefreshPromo(ref responseText, "front", transactionId, computerId, decimalDigit, myConn);
            _posModule.OrderDetail_CalBill(ref responseText, transactionId, computerId, shopId, decimalDigit, "front", myConn);

            DataSet resultData = new DataSet();
            var isSuccess = _posModule.BillDetail(ref responseText, ref receiptHtml, ref copyReceiptString, ref noCopy, ref resultData,
                (int)ViewBillTypes.Default, transactionId, computerId, shopId, 0, "front", langId, 1, myConn);
            if (isSuccess)
            {
                receiptHtml = receiptHtml.Replace("<body>", "<body style='max-width:300px; margin:auto;'>");
            }
            else
            {
                receiptHtml = responseText;
            }
            return receiptHtml;
        }

        public async Task<DataTable> GetModifierOrderAsync(IDbConnection conn, int shopId, int transactionId, int computerId, int parentOrderDetailId, string productCode = "", SaleModes saleMode = SaleModes.DineIn)
        {
            DataTable dtComment = await _posRepo.GetProductFixModifierAsync(conn, shopId, productCode, saleMode);
            DataSet orderDataSet = await _posRepo.GetOrderDataAsync(conn, transactionId, computerId);

            var orders = (from order in orderDataSet.Tables["Orders"].AsEnumerable()
                          where order.GetValue<int>("OrderDetailLinkID") == parentOrderDetailId
                          select order).ToList();

            var modifierOrder = (from commentProduct in dtComment.AsEnumerable()
                                 join order in orders
                                 on commentProduct.GetValue<int>("ProductID") equals order.GetValue<int>("ProductID") into gj
                                 from orderComment in gj.DefaultIfEmpty()
                                 select new
                                 {
                                     ProductID = commentProduct.GetValue<int>("ProductID"),
                                     ProductDeptID = commentProduct.GetValue<int>("ProductDeptID"),
                                     ProductTypeID = commentProduct.GetValue<int>("ProductTypeID"),
                                     OtherProductTypeID = 0,
                                     ProductCode = commentProduct.GetValue<string>("ProductCode"),
                                     ProductName = commentProduct.GetValue<string>("ProductName"),
                                     ProductName1 = commentProduct.GetValue<string>("ProductName1"),
                                     ProductName2 = commentProduct.GetValue<string>("ProductName2"),
                                     ProductName3 = commentProduct.GetValue<string>("ProductName3"),
                                     TransactionID = transactionId,
                                     ComputerID = computerId,
                                     OrderDetailID = orderComment.GetValue<int>("OrderDetailID"),
                                     OrderDetailLinkID = parentOrderDetailId,
                                     IndentLevel = orderComment != null ? orderComment.GetValue<int>("IndentLevel") : 1,
                                     TotalQty = orderComment.GetValue<double>("TotalQty"),
                                     ProductPrice = commentProduct.GetValue<decimal>("ProductPrice"),
                                     RequireAddAmount = commentProduct.GetValue<int>("RequireAddAmount"),
                                     DisplayMobile = commentProduct.GetValue<int>("DisplayMobile")
                                 }).ToList();

            var manualModifierOrder = (from order in orders
                                       where order.GetValue<int>("OrderDetailLinkID") > 0
                                       && order.GetValue<decimal>("PricePerUnit") == 0
                                       && order.GetValue<int>("ProductID") == 0
                                       select new
                                       {
                                           ProductID = 0,
                                           ProductDeptID = 0,
                                           ProductTypeID = 14,
                                           OtherProductTypeID = 14,
                                           ProductCode = "",
                                           ProductName = order.GetValue<string>("ProductName"),
                                           ProductName1 = order.GetValue<string>("ProductName1"),
                                           ProductName2 = order.GetValue<string>("ProductName2"),
                                           ProductName3 = order.GetValue<string>("ProductName3"),
                                           TransactionID = transactionId,
                                           ComputerID = computerId,
                                           OrderDetailID = order.GetValue<int>("OrderDetailID"),
                                           OrderDetailLinkID = parentOrderDetailId,
                                           IndentLevel = order != null ? order.GetValue<int>("IndentLevel") : 1,
                                           TotalQty = order.GetValue<double>("TotalQty"),
                                           ProductPrice = order.GetValue<decimal>("PricePerUnit"),
                                           RequireAddAmount = 0,
                                           DisplayMobile = 0
                                       }).FirstOrDefault();
            if (manualModifierOrder != null)
                modifierOrder.Add(manualModifierOrder);
            return modifierOrder.ToDataTable();
        }

        public async Task<object> GetOrderDataAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int langId = 1)
        {
            var myConn = conn as MySqlConnection;

            object dataSet = null;
            string responseText = "";
            var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);

            _posModule.OrderDetail_RefreshPromo(ref responseText, "front", transactionId, computerId, decimalDigit, myConn);
            _posModule.OrderDetail_CalBill(ref responseText, transactionId, computerId, shopId, decimalDigit, "front", myConn);

            var ds = await _posRepo.GetOrderDataAsync(conn, transactionId, computerId, langId);

            dataSet = (from bill in ds.Tables["Bill"].AsEnumerable()
                       select new
                       {
                           TransactionID = bill.GetValue<int>("TransactionID"),
                           ComputerID = bill.GetValue<int>("ComputerID"),
                           TransactionStatusID = bill.GetValue<int>("TransactionStatusID"),
                           SaleMode = bill.GetValue<int>("SaleMode"),
                           TransactionName = bill.GetValue<string>("TransactionName"),
                           ReceiptNumber = bill.GetValue<string>("ReceiptNumber"),
                           SaleDate = bill.GetValue<string>("SaleDate"),
                           ShopID = bill.GetValue<int>("ShopID"),
                           TransactionVAT = bill.GetValue<double>("TransactionVAT"),
                           TransactionVATable = bill.GetValue<decimal>("TransactionVATable"),
                           TransactionBeforeVAT = bill.GetValue<decimal>("TranBeforeVAT"),
                           VATPercent = bill.GetValue<double>("VATPercent"),
                           ServiceChargePercent = bill.GetValue<double>("ServiceChargePercent"),
                           ServiceCharge = bill.GetValue<decimal>("ServiceCharge"),
                           ServiceChargeVAT = bill.GetValue<double>("ServiceChargeVAT"),
                           ServiceChargeBeforeVAT = bill.GetValue<decimal>("SCBeforeVAT"),
                           ReceiptTotalQty = bill.GetValue<double>("ReceiptTotalQty"),
                           ReceiptRetailPrice = bill.GetValue<decimal>("ReceiptRetailPrice"),
                           ReceiptDiscount = bill.GetValue<decimal>("ReceiptDiscount"),
                           ReceiptSalePrice = bill.GetValue<decimal>("ReceiptSalePrice"),
                           ReceiptNetSale = bill.GetValue<decimal>("ReceiptNetSale"),
                           ReceiptPayPrice = bill.GetValue<decimal>("ReceiptPayPrice"),
                           ReceiptRoundingBill = bill.GetValue<double>("ReceiptRoundingBill"),
                           MemberID = bill.GetValue<int>("MemberID"),
                           MemberName = bill.GetValue<string>("MemberName"),
                           TotalBillCheck = bill.GetValue<int>("NoPrintBillDetail"),
                           TableID = bill.GetValue<int>("TableID"),
                           TableName = bill.GetValue<string>("TableName"),
                           VATDesp = bill.GetValue<string>("VATDesp"),
                           ReceiptGrandTotal = bill.GetValue<decimal>("ReceiptGrandTotal"),
                           ReceiptVAT = bill.GetValue<double>("ReceiptVAT"),
                           ReferenceNo = bill.GetValue<string>("ReferenceNo"),
                           Orders = (from order in ds.Tables["Orders"].AsEnumerable()
                                     select new
                                     {
                                         TransactionID = bill.GetValue<int>("TransactionID"),
                                         ComputerID = bill.GetValue<int>("ComputerID"),
                                         OrderDetailID = order.GetValue<int>("OrderDetailID"),
                                         OrderDetailLinkID = order.GetValue<int>("OrderDetailLinkID"),
                                         IndentLevel = order.GetValue<int>("IndentLevel"),
                                         ProductID = order.GetValue<int>("ProductID"),
                                         ProductTypeID = order.GetValue<int>("ProductSetType"),
                                         OrderStatusID = order.GetValue<int>("OrderStatusID"),
                                         SaleMode = order.GetValue<int>("SaleMode"),
                                         TotalQty = order.GetValue<double>("TotalQty"),
                                         PricePerUnit = order.GetValue<decimal>("PricePerUnit"),
                                         TotalRetailPrice = order.GetValue<decimal>("TotalRetailPrice"),
                                         SalePrice = order.GetValue<decimal>("SalePrice"),
                                         VATDisplay = order.GetValue<string>("VATDisplay"),
                                         ProductCode = order.GetValue<string>("ProductCode"),
                                         ProductName = order.GetValue<string>("ProductName"),
                                         ProductName1 = order.GetValue<string>("ProductName1"),
                                         ProductDisplayName = string.IsNullOrEmpty(order.GetValue<string>("ProductDisplayName")) ? order.GetValue<string>("ProductName1") : order.GetValue<string>("ProductDisplayName"),
                                         VATType = order.GetValue<int>("VATType"),
                                         IsComment = order.GetValue<int>("IsComment"),
                                         NetSale = order.GetValue<decimal>("NetSale"),
                                         ProductVAT = order.GetValue<double>("ProductVAT"),
                                         ProductBeforeVAT = order.GetValue<decimal>("ProductBeforeVAT"),
                                         ServiceCharge = order.GetValue<decimal>("SCAmount"),
                                         ServiceChargeVAT = order.GetValue<double>("SCVAT"),
                                         ServiceChargeBeforeVAT = order.GetValue<decimal>("SCBeforeVAT"),
                                         Vatable = order.GetValue<decimal>("Vatable"),
                                         ItemDiscAllow = order.GetValue<int>("ItemDiscAllow"),
                                         PrintStatus = order.GetValue<int>("PrintStatus"),
                                         CurrentStock = order.GetValue<double?>("CurrentStock"),
                                     }).ToList()
                       }).FirstOrDefault();
            return dataSet;
        }

        public async Task<List<OrderDetail>> GetOrderDetailsAsync(IDbConnection conn, int transactionId, int computerId, int shopId, int staffId = 2, int langId = 1)
        {
            List<OrderDetail> orderDetails = new List<OrderDetail>();
            var dsOrder = await _posRepo.GetOrderDataAsync(conn, transactionId, computerId, langId);
            var dtOrder = dsOrder.Tables["Orders"];
            foreach (DataRow row in dtOrder.Rows)
            {
                var order = new OrderDetail()
                {
                    TransactionID = row.GetValue<int>("TransactionID"),
                    ComputerID = row.GetValue<int>("ComputerID"),
                    TerminalID = row.GetValue<int>("OrderComputerID"),
                    ShopID = shopId,
                    // TODO: OrderStaffID = staffId,
                    OrderDetailLinkID = row.GetValue<int>("OrderDetailLinkID"),
                    OrderDetailID = row.GetValue<int>("OrderDetailID"),
                    OrderStatusID = row.GetValue<int>("OrderStatusID"),
                    SaleMode = (SaleModes)row.GetValue<int>("SaleMode"),
                    IndentLevel = row.GetValue<int>("IndentLevel"),
                    ProductID = row.GetValue<int>("ProductID"),
                    ProductCode = row.GetValue<string>("ProductCode"),
                    ProductName = row.GetValue<string>("ProductName"),
                    ProductName1 = row.GetValue<string>("ProductName1"),
                    ProductName2 = row.GetValue<string>("ProductName2"),
                    ProductName3 = row.GetValue<string>("ProductName3"),
                    //ProductDisplayName = string.IsNullOrEmpty(row.GetValue<string>("ProductDisplayName")) ? row.GetValue<string>("ProductName") : row.GetValue<string>("ProductDisplayName"),
                    ProductDisplayName = row.GetValue<string>("ProductDisplayName"),
                    ProductTypeID = row.GetValue<int>("ProductSetType"),
                    TotalQty = row.GetValue<double>("TotalQty"),
                    CurrentStock = row["CurrentStock"] as int?,
                    EnableCountDownStock = row["CurrentStock"] != DBNull.Value,
                    ParentProductID = row.GetValue<int>("ParentProductID"),
                    PGroupID = row.GetValue<int>("PGroupID"),
                    OtherFoodName = row.GetValue<string>("OtherFoodName"),
                    SetGroupNo = row.GetValue<int>("SetGroupNo"),
                    QtyRatio = row.GetValue<double>("QtyRatio"),
                    PrintStatus = row.GetValue<int>("PrintStatus")
                };
                if (order.ProductID == 0 && string.IsNullOrEmpty(order.OtherFoodName))
                {
                    order.OtherFoodName = order.ProductDisplayName;
                }
                orderDetails.Add(order);
            }
            return orderDetails;
        }

        public async Task ModifyOrderAsync(IDbConnection conn, OrderDetail orderDetail)
        {
            await EditOrderAsync(conn, orderDetail, OrdersModifyTypes.ModifyQty);
            orderDetail.TotalQty = orderDetail.ToQty;
        }
        //TODO: handle print kds
        public async Task<DataSet> MoveOrderAsync(IDbConnection conn, TableManage tableManage)
        {
            var myConn = conn as MySqlConnection;

            string saleDate = await _posRepo.GetSaleDateAsync(conn, tableManage.ShopID, true);
            var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);

            await OpenTransactionAsync(conn, new OrderTransaction()
            {
                ShopID = tableManage.ShopID,
                TerminalID = tableManage.TerminalID,
                SaleDate = saleDate,
                StaffID = tableManage.StaffID,
                SaleMode = tableManage.SaleMode,
                TableID = tableManage.ToTableID
            });

            string responseText = "";
            DataSet resultData = new DataSet();
            DataTable dtOrders = tableManage.Orders.ToDataTable();
            bool isSuccess = _posModule.Table_MoveOrder(ref responseText, ref resultData, (int)OrdersManagementActions.MoveOrderItem,
                "front", tableManage.FromTableID, tableManage.ToTableID, dtOrders, tableManage.TerminalID,
                tableManage.ShopID, saleDate, tableManage.StaffID, tableManage.LangID, tableManage.ReasonList,
                tableManage.ReasonText, decimalDigit, myConn);
            if (!isSuccess)
                throw new VtecPOSException(responseText);
            return resultData;
        }

        public async Task<bool> SubmitOrderAsync(IDbConnection conn, Transaction transaction)
        {
            string responseText = string.Empty;
            DataSet resultData = new DataSet();
            try
            {
                string saleDate = await _posRepo.GetSaleDateAsync(conn, transaction.ShopID, true);
                bool isSuccess = _posModule.KDS_Submit(ref responseText, ref resultData, transaction.TransactionID,
                    transaction.ComputerID, transaction.ShopID, saleDate, "front", conn as MySqlConnection);
                if (!isSuccess)
                    throw new VtecPOSException(string.IsNullOrEmpty(responseText) ? "An error occurred at SubmitOrders " : responseText);

                await _posRepo.SetComputerAccessAsync(conn, transaction.TableID, 0);
                return isSuccess;
            }
            catch (Exception ex)
            {
                throw new VtecPOSException(string.IsNullOrEmpty(responseText) ? "An error occurred at SubmitOrders" + ex.Message : responseText);
            }
        }

        public async Task<bool> SubmitSaleModeOrderAsync(IDbConnection conn, Transaction transaction)
        {
            try
            {
                var sqlUpdate = "update ordertransactionfront" +
                    " set TransactionStatusID=@status";
                var cmd = _database.CreateCommand(conn);

                if (!string.IsNullOrEmpty(transaction.TransactionName))
                {
                    sqlUpdate += ",TableName=@tableName";
                    cmd.Parameters.Add(_database.CreateParameter("@tableName", transaction.TransactionName));
                }
                if (!string.IsNullOrEmpty(transaction.TransactionName))
                {
                    sqlUpdate += ",TransactionName=@transactionName";
                    cmd.Parameters.Add(_database.CreateParameter("@transactionName", transaction.TransactionName));
                }
                if (transaction.TotalCustomer > 0)
                {
                    sqlUpdate += ",NoCustomer=@totalCustomer";
                    cmd.Parameters.Add(_database.CreateParameter("@totalCustomer", transaction.TotalCustomer));
                }
                sqlUpdate += " where TransactionID=@transactionId and ComputerID=@computerId";
                cmd.CommandText = sqlUpdate;
                cmd.Parameters.Add(_database.CreateParameter("@status", (int)transaction.TransactionStatus));
                cmd.Parameters.Add(_database.CreateParameter("@transactionId", transaction.TransactionID));
                cmd.Parameters.Add(_database.CreateParameter("@computerId", transaction.TerminalID));
                await _database.ExecuteNonQueryAsync(cmd);
            }
            catch(Exception ex)
            {
                throw new VtecPOSException(ex.Message);
            }
            return true;
        }

        public async Task OpenTransactionAsync(IDbConnection conn, OrderTransaction tranData)
        {
            var myConn = conn as MySqlConnection;
            if (tranData.TransactionID == 0)
            {
                if (await _posRepo.GetOpenedTableTransactionAsync(conn, tranData))
                    return;
            }

            var saleDate = await _posRepo.GetSaleDateAsync(conn, tranData.ShopID, true);

            string responseText = "";
            int transactionId = 0;
            int computerId = tranData.TerminalID;
            string tranKey = "";
            string queueNo = "";

            bool isSuccess = _posModule.Tran_Open(ref responseText, ref transactionId, ref computerId, ref tranKey,
                "front", tranData.ShopID, saleDate, tranData.StaffID, tranData.TerminalID, (int)tranData.SaleMode,
                tranData.TransactionName, tranData.QueueName, tranData.TotalCustomer, tranData.TableID, (int)tranData.TransactionStatus, myConn);

            if (isSuccess)
            {
                tranData.SaleDate = saleDate;
                tranData.TransactionID = transactionId;
                tranData.ComputerID = computerId;
                tranData.QueueNo = queueNo;

                _posModule.Table_UpdateStatus(ref responseText, "front", transactionId, computerId, tranData.ShopID,
                    saleDate, tranData.LangID, myConn);

                await _posRepo.AddQuestionAsync(conn, tranData);

                await _posRepo.SetComputerAccessAsync(conn, tranData.TableID, tranData.TerminalID);
                //TODO: need update opentime?
                //try
                //{
                //    var cmd = _database.CreateCommand("update ordertransactionfront set OpenTime=Now() where TransactionID=@tranId and ComputerID=@compId", conn);
                //    cmd.Parameters.Add(_database.CreateParameter("@tranId", transactionId));
                //    cmd.Parameters.Add(_database.CreateParameter("@compId", computerId));
                //    cmd.ExecuteNonQuery();
                //}
                //catch (Exception) { }
            }
            else
            {
                throw new VtecPOSException($"An error occurred when open transaction {responseText}");
            }
        }

        async Task EditOrderAsync(IDbConnection conn, OrderDetail orderData, OrdersModifyTypes modifyType)
        {
            string responseText = "";
            string saleDate = await _posRepo.GetSaleDateAsync(conn, orderData.ShopID, true);
            int decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
            bool isSuccess = _posModule.OrderDetail_Modify(ref responseText, true, false, (int)modifyType, orderData.OrderDetailID,
                orderData.TransactionID, orderData.ComputerID, orderData.ShopID, saleDate, orderData.FromQty,
                orderData.ToQty, orderData.OrderStaffID, orderData.OrderStaffID, orderData.OrderTableID,
                orderData.OrderTableID, orderData.OrderDetailID, orderData.TransactionID, orderData.ComputerID,
                "front", decimalDigit, orderData.ModifyReasonIdList, orderData.ModifyReasonText, conn as MySqlConnection);
            if (isSuccess == false)
                throw new VtecPOSException(responseText);
        }

        public Task<DataTable> GetChildOrderAsync(IDbConnection conn, int transactionId, int computerId, int parentOrderId)
        {
            IDbCommand cmd = _database.CreateCommand("select * from OrderDetailfront " +
                " where TransactionID=@transactionId" +
                " and ComputerID=@computerId" +
                " and OrderDetailLinkID=@parentOrderId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            cmd.Parameters.Add(_database.CreateParameter("@parentOrderId", parentOrderId));

            var dataSet = new DataSet();
            IDataAdapter adapter = _database.CreateDataAdapter(cmd);
            adapter.Fill(dataSet);
            adapter.TableMappings.Add("Table", "Orders");
            return Task.FromResult(dataSet.Tables[0]);
        }

        public Task DeleteChildComboAsync(IDbConnection conn, int transactionId, int computerId, int orderDetailId)
        {
            var responseText = "";
            var isSuccess = _posModule.OrderDetail_delCombo(ref responseText, orderDetailId, transactionId, computerId, conn as MySqlConnection);
            if (!isSuccess)
                throw new VtecPOSException(responseText);
            return Task.FromResult(isSuccess);
        }
        //TODO: must call PrintKdsDataFromDataSet
        public Task<DataSet> MoveTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, string saleDate, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText)
        {
            return ManageTableOrder(conn, OrdersManagementActions.MoveTable, transactionId, computerId, shopId, saleDate, staffId, langId, toTableIdList, modifyReasonIdList, modifyReasonText);
        }
        //TODO: must call PrintKdsDataFromDataSet
        public Task<DataSet> MergeTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, string saleDate, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText)
        {
            return ManageTableOrder(conn, OrdersManagementActions.MergeTable, transactionId, computerId, shopId, saleDate, staffId, langId, toTableIdList, modifyReasonIdList, modifyReasonText);
        }
        //TODO: must call PrintKdsDataFromDataSet
        public Task<DataSet> SplitTableOrderAsync(IDbConnection conn, int transactionId, int computerId, int shopId, string saleDate, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText)
        {
            return ManageTableOrder(conn, OrdersManagementActions.SplitTable, transactionId, computerId, shopId, saleDate, staffId, langId, toTableIdList, modifyReasonIdList, modifyReasonText);
        }

        private Task<DataSet> ManageTableOrder(IDbConnection conn, OrdersManagementActions action, int transactionId, int computerId, int shopId, string saleDate, int staffId, int langId, string toTableIdList, string modifyReasonIdList, string modifyReasonText)
        {
            var responseText = "";
            var printData = new DataSet();
            bool isSuccess = _posModule.Table_MoveTable(ref responseText, ref printData, (int)action, "front", transactionId, computerId, shopId, saleDate,
                staffId, langId, toTableIdList, modifyReasonIdList, modifyReasonText, conn as MySqlConnection);
            if (!isSuccess)
                throw new VtecPOSException(responseText);
            return Task.FromResult(printData);
        }

        public Task<DataSet> GetBillDetail(IDbConnection conn, int transactionId, int computerId, int shopId, int langId)
        {
            var responseText = "";
            var receiptText = "";
            var copyReceiptText = "";
            var noPrintCopy = 0;
            var dsPrintData = new DataSet();
            var isSuccess = _posModule.BillDetail(ref responseText, ref receiptText, ref copyReceiptText, ref noPrintCopy, ref dsPrintData, (int)ViewBillTypes.Print, transactionId, computerId, shopId, 0, "front", langId, 1, conn as MySqlConnection);
            if (!isSuccess)
                throw new VtecPOSException(responseText);
            return Task.FromResult(dsPrintData);
        }
    }
}
