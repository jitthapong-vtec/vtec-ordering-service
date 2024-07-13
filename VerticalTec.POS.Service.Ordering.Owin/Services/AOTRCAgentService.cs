using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using RCAgentAOTRR;
using RCAgentAOTRR.Response;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public class AOTRCAgentService
    {
        static readonly NLog.Logger _log = NLog.LogManager.GetLogger("logpayment");

        public string RCAgentPath => AppConfig.Instance.RCAgentPath;

        private readonly IDatabase _database;
        private readonly VtecPOSRepo _vtecPOSRepo;
        private RCAgent _rcAgent;
        private RCConfig _rcConfig;

        public RCAgent RCAgent => _rcAgent;

        public AOTRCAgentService(IDatabase database)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolveEventHandler);

            _database = database;
            _vtecPOSRepo = new VtecPOSRepo(database);
        }

        public ReturnOfSendLoginStatus SendLoginStatus(int shopId, int computerId, int staffId)
        {
            InitRCAgent(shopId, computerId);

            var loginResp = _rcAgent.SendLoginStatus(DateTime.Today);
            try
            {
                _log.Log(NLog.LogLevel.Info, $"SendLoginStatus => {JsonConvert.SerializeObject(loginResp)}");
            }
            catch { }
            return loginResp;
        }

        public ReturnOfSendLogoutStatus SendLogoutStatus(int shopId, int computerId, int staffId)
        {
            var logoutResp = _rcAgent.SendLogoutStatus();
            try
            {
                _log.Log(NLog.LogLevel.Info, $"SendLogoutStatus => {JsonConvert.SerializeObject(logoutResp)}");
            }
            catch { }
            return logoutResp;
        }

        public ReturnOfRequestRcCode RequestRcCode(int shopId, int transactionId, int computerId)
        {
            using (var conn = (MySqlConnection)_database.Connect())
            {
                var ds = GetTransactionData(transactionId, computerId, conn, "");
                var dtTrans = ds.Tables["OrderTransaction"];
                var dtOrderDetail = ds.Tables["OrderDetail"];
                var dtPayDetail = ds.Tables["OrderPayDetail"];
                var dtShopData = ds.Tables["ShopData"];

                if (dtTrans.Rows.Count == 0)
                {
                    ds = GetTransactionData(transactionId, computerId, conn, "front");
                    dtTrans = ds.Tables["OrderTransaction"];
                    dtOrderDetail = ds.Tables["OrderDetail"];
                    dtPayDetail = ds.Tables["OrderPayDetail"];
                }

                if (dtTrans.Rows.Count == 0)
                {
                    var errMsg = $"No data of TransactionID={transactionId}, ComputerID={computerId}";
                    _log.Error(errMsg);
                    throw new Exception(errMsg);
                }

                var shopData = dtShopData.AsEnumerable().Select(r => new
                {
                    VATType = r.GetValue<int>("VATType"),
                    VATPercent = r.GetValue<decimal>("ProductVATPercent")
                }).First();

                var orderTran = dtTrans.AsEnumerable().Select(r => new
                {
                    ReceiptNumber = r.GetValue<string>("ReceiptNumber"),
                    SaleDate = r.GetValue<DateTime>("SaleDate"),
                    PaidTime = r.GetValue<DateTime>("PaidTime"),
                    VoidTime = r.GetValue<DateTime>("VoidTime"),
                    VoidReason = r.GetValue<string>("VoidReason"),
                    TransactionVAT = r.GetValue<decimal>("TransactionVAT"),
                    TransactionVATable = r.GetValue<decimal>("TransactionVATable"),
                    TranBeforeVAT = r.GetValue<decimal>("TranBeforeVAT"),
                    VATPercent = r.GetValue<decimal>("VATPercent"),
                    ProductVAT = r.GetValue<decimal>("ProductVAT"),
                    ServiceChargePercent = r.GetValue<decimal>("ServiceChargePercent"),
                    ServiceCharge = r.GetValue<decimal>("ServiceCharge"),
                    ServiceChargeVAT = r.GetValue<decimal>("ServiceChargeVAT"),
                    ServiceChargeBeforeVAT = r.GetValue<decimal>("SCBeforeVAT"),
                    ReceiptTotalQty = r.GetValue<double>("ReceiptTotalQty"),
                    ReceiptRetailPrice = r.GetValue<decimal>("ReceiptRetailPrice"),
                    ReceiptDiscount = r.GetValue<decimal>("ReceiptDiscount"),
                    ReceiptSalePrice = r.GetValue<decimal>("ReceiptSalePrice"),
                    ReceiptNetSale = r.GetValue<decimal>("ReceiptNetSale"),
                    ReceiptPayPrice = r.GetValue<decimal>("ReceiptPayPrice"),
                    ReceiptRoudingBill = r.GetValue<decimal>("ReceiptRoundingBill"),
                    DiscountItem = r.GetValue<decimal>("DiscountItem"),
                    DiscountBill = r.GetValue<decimal>("DiscountBill"),
                    DiscountOther = r.GetValue<decimal>("DiscountOther"),
                    TotalDiscount = r.GetValue<decimal>("TotalDiscount"),
                    ReferenceNo = r.GetValue<string>("ReferenceNo"),
                    TransactionStatusID = r.GetValue<int>("TransactionStatusID"),
                }).First();

                var isVoid = new int[] { 9, 99 }.Contains(orderTran.TransactionStatusID);

                var receiptStatus = "1";
                if (isVoid)
                    receiptStatus = "2";

                var totalIncVat = orderTran.ReceiptRetailPrice;
                var totalVat = (totalIncVat * orderTran.VATPercent / (100 + orderTran.VATPercent));
                var totalExcVat = totalIncVat - totalVat;
                var discountIncVat = orderTran.ReceiptDiscount;
                var discountVat = orderTran.ReceiptDiscount * orderTran.VATPercent / (100 + orderTran.VATPercent);
                var discountExcVat = discountIncVat - discountVat;
                var extraDiscountIncVat = orderTran.DiscountOther;
                var extraDiscountVat = orderTran.DiscountOther * orderTran.VATPercent / (100 + orderTran.VATPercent);
                var extraDiscountExcVat = extraDiscountIncVat - extraDiscountVat;
                var payAmount = (double)dtPayDetail.AsEnumerable().Sum(r => r.GetValue<decimal>("PayAmount"));
                var cashChange = (double)dtPayDetail.AsEnumerable().Sum(r => r.GetValue<decimal>("CashChange"));
                var received = payAmount + cashChange;

                var rc = new Receipt();
                rc.companyCode = _rcConfig.companyCode;
                rc.ipAddress = _rcConfig.posIPAddress;
                rc.posName = _rcConfig.posName;
                rc.rdId = _rcConfig.rdId;
                rc.shopId = _rcConfig.rdId;
                rc.transactionDatetime = orderTran.PaidTime;
                rc.receiptDate = orderTran.SaleDate;
                rc.receiptType = "1";
                rc.receiptStatus = receiptStatus;
                rc.taxInvoice = orderTran.ReceiptNumber;
                rc.refNo = orderTran.ReferenceNo;
                rc.totalExcVat = (double)Math.Round(totalExcVat, 2);
                rc.totalVat = (double)Math.Round(totalVat, 2);
                rc.totalIncVat = (double)totalIncVat;
                rc.discountIncVat = (double)discountIncVat;
                rc.discountVat = (double)Math.Round(discountVat, 2);
                rc.discountExcVat = (double)Math.Round(discountExcVat, 2);
                rc.extraDiscountIncVat = (double)extraDiscountIncVat;
                rc.extraDiscountVat = (double)Math.Round(extraDiscountVat, 2);
                rc.extraDiscountExcVat = (double)Math.Round(extraDiscountExcVat, 2);
                rc.serviceChargeIncVat = (double)(orderTran.ServiceCharge + orderTran.ServiceChargeVAT);
                rc.serviceChargeVat = (double)orderTran.ServiceChargeVAT;
                rc.netIncVat = (double)orderTran.ReceiptNetSale;
                rc.netExcVat = (double)(orderTran.ReceiptNetSale - orderTran.TransactionVAT);
                rc.netVat = (double)orderTran.TransactionVAT;
                rc.round = (double)orderTran.ReceiptRoudingBill;
                rc.vatRate = (double)orderTran.VATPercent;
                rc.received = received;
                rc.change = cashChange;
                rc.totalText = ResCenterObjLib.ResCenterLib.AmountThaiBaht(received.ToString());

                if (isVoid)
                {
                    rc.cancelTaxInvoice = orderTran.ReceiptNumber;
                    rc.cancelTaxInvoiceDate = orderTran.VoidTime;
                    rc.cancelTaxInvoicePosName = rc.posName;
                    rc.voidReason = orderTran.VoidReason;
                    rc.totalIncVat = rc.totalIncVat * -1;
                    rc.totalVat = rc.totalVat * -1;
                    rc.received = rc.received * -1;
                    rc.change = rc.change * -1;
                }

                rc.receiptItems = dtOrderDetail.AsEnumerable().Select(r =>
                {
                    var receiptItem = new ReceiptItem
                    {
                        itemNo = r.GetValue<int>("OrderDetailID"),
                        productCode = r.GetValue<string>("ProductCode"),
                        productName = r.GetValue<string>("ProductName"),
                        quantity = r.GetValue<double>("TotalQty"),
                        serviceCharge = r.GetValue<double>("SCAmount"),
                        vatType = r.GetValue<string>("VATType"),
                        vatRate = r.GetValue<double>("ProductVATPercent"),
                        unitDiscountPercent = r.GetValue<double>("DiscPercent"),
                        unitPriceIncVat = r.GetValue<double>("PricePerUnit"),
                        unitPriceVat = Math.Round(r.GetValue<double>("PricePerUnit") * r.GetValue<double>("ProductVATPercent") / (100 + r.GetValue<double>("ProductVATPercent")), 2),
                        totalIncVat = r.GetValue<double>("TotalRetailPrice"),
                        totalVat = r.GetValue<double>("TotalRetailVAT"),
                        totalDiscountIncVat = r.GetValue<double>("TotalDiscount"),
                        totalDiscountVat = r.GetValue<double>("DiscVAT"),
                        totalNetIncVat = r.GetValue<double>("NetSale"),
                        totalDisplay = r.GetValue<double>("TotalRetailPrice"),
                        totalNetVat = r.GetValue<double>("ProductVAT")
                    };

                    if (isVoid)
                    {
                        receiptItem.quantity = receiptItem.quantity * -1;
                        receiptItem.unitPriceIncVat = receiptItem.unitPriceIncVat * -1;
                        receiptItem.totalVat = receiptItem.totalVat * -1;
                        receiptItem.serviceCharge = receiptItem.serviceCharge * -1;
                    }

                    return receiptItem;
                }).ToList();

                rc.receiptPayments = dtPayDetail.AsEnumerable().Select(r =>
                {
                    var receiptPayment = new ReceiptPayment
                    {
                        paymentNo = r.GetValue<int>("PayDetailID"),
                        paymentCurrency = r.GetValue<string>("CurrencyCode"),
                        paymentAmount = r.GetValue<double>("PayAmount"),
                        paymentType = r.GetValue<string>("PayTypeName")
                    };

                    if (isVoid)
                    {
                        receiptPayment.paymentAmount = receiptPayment.paymentAmount * -1;
                    }

                    return receiptPayment;
                }).ToList();

                var reqJson = JsonConvert.SerializeObject(rc);
                _log.Log(NLog.LogLevel.Info, $"RequestRcCode => {reqJson}");

                var rcCode = _rcAgent.RequestRcCode(rc);

                _log.Log(NLog.LogLevel.Info, $"Response RequestRcCode => {JsonConvert.SerializeObject(rcCode)}");
                return rcCode;
            }
        }

        public List<RCAgentAOTRR.Models.AnnouncementModel> GetLatestAnnouncements()
        {
            return _rcAgent.GetLatestAnnouncements();
        }

        private DataSet GetTransactionData(int transactionId, int computerId, MySqlConnection conn, string tableSubfix = "front")
        {
            var cmd = new MySqlCommand($@"select * from ordertransaction{tableSubfix} where TransactionID=@tid and ComputerID=@cid;
            select a.*, b.ProductCode, b.ProductName from orderdetail{tableSubfix} a left outer join products b on a.ProductID=b.ProductID where a.TransactionID=@tid and a.ComputerID=@cid and OrderStatusID=2;
            select a.*, b.PayTypeName from orderpaydetail{tableSubfix} a left outer join paytype b on a.PayTypeID=b.PayTypeID where a.TransactionID=@tid and a.ComputerID=@cid;
            select * from shop_data a left outer join productvat b on a.VATCode=b.ProductVATCode;", conn);
            cmd.Parameters.Add(new MySqlParameter("@tid", transactionId));
            cmd.Parameters.Add(new MySqlParameter("@cid", computerId));

            var ds = new DataSet();
            var adapter = new MySqlDataAdapter(cmd);
            adapter.Fill(ds);
            ds.Tables[0].TableName = "OrderTransaction";
            ds.Tables[1].TableName = "OrderDetail";
            ds.Tables[2].TableName = "OrderPayDetail";
            ds.Tables[3].TableName = "ShopData";
            return ds;
        }

        public ReturnOfConfirmPrintRcCode ConfirmPrintRcCode(string rcCode)
        {
            var resp = _rcAgent.ConfirmPrintRcCode(rcCode);
            return resp;
        }

        private void InitRCAgent(int shopId, int computerId)
        {
            using (var conn = _database.Connect())
            {
                var aotServerUrl = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotServerUrl", shopId: shopId, computerId: computerId);
                var companyCode = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotCompanyCode", shopId: shopId, computerId: computerId);
                var posName = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotPosName", shopId: shopId, computerId: computerId);
                var posId = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotPosID", shopId: shopId, computerId: computerId);
                var shopCode = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotShopCode", shopId: shopId, computerId: computerId);
                var clientId = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotClientID", shopId: shopId, computerId: computerId);
                var clientSecret = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotClientSecret", shopId: shopId, computerId: computerId);
                var ipAddress = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotPosIPAddress", shopId: shopId, computerId: computerId);

                //var host = Dns.GetHostEntry(Dns.GetHostName());
                //var ipAddress = host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault()?.ToString();

                _rcConfig = new RCConfig();
                _rcConfig.rcServerUrl = aotServerUrl.Result;
                _rcConfig.companyCode = companyCode.Result;
                _rcConfig.posIPAddress = ipAddress.Result;
                _rcConfig.posName = posName.Result;
                _rcConfig.posId = posId.Result;
                _rcConfig.shopCode = shopCode.Result;
                _rcConfig.clientId = clientId.Result;
                _rcConfig.clientSecret = clientSecret.Result;

                _log.Log(NLog.LogLevel.Info, $"RCConfig => {JsonConvert.SerializeObject(_rcConfig)}");
            }

            _rcAgent = new RCAgent(_rcConfig);
        }

        private Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(RCAgentPath))
                throw new Exception("Please config path to RCAgent!!!");
            var assemblyName = new AssemblyName(args.Name);
            return Assembly.LoadFile(Path.Combine(RCAgentPath, $"{assemblyName.Name}.dll"));
        }
    }
}
