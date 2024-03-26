using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using RCAgentAOTRR;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

        public AOTRCAgentService(IDatabase database)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolveEventHandler);

            _database = database;
            _vtecPOSRepo = new VtecPOSRepo(database);
        }

        public dynamic SendLoginStatus(int shopId, int computerId, int staffId)
        {
            InitRCAgent(shopId, computerId);

            var loginResp = _rcAgent.SendLoginStatus(DateTime.Now);
            return loginResp;
        }

        public dynamic SendLogoutStatus(int shopId, int computerId, int staffId)
        {
            var logoutResp = _rcAgent.SendLogoutStatus();
            return logoutResp;
        }

        public dynamic RequestRcCode(int shopId, int transactionId, int computerId)
        {
            using (var conn = (MySqlConnection)_database.Connect())
            {
                var ds = GetTransactionData(transactionId, computerId, conn, "front");
                var dtTrans = ds.Tables["OrderTransaction"];
                var dtOrderDetail = ds.Tables["OrderDetail"];
                var dtPayDetail = ds.Tables["OrderPayDetail"];

                if (dtTrans.Rows.Count == 0)
                {
                    ds = GetTransactionData(transactionId, computerId, conn, "");
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

                var orderTran = dtTrans.AsEnumerable().Select(r => new
                {
                    ReceiptNumber = r.GetValue<string>("ReceiptNumber"),
                    SaleDate = r.GetValue<DateTime>("SaleDate"),
                    PaidTime = r.GetValue<DateTime>("PaidTime"),
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

                var receiptStatus = "1";
                if (orderTran.TransactionStatusID == 9)
                    receiptStatus = "2";

                var rc = new RCAgentAOTRR.Receipt();
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
                rc.totalExcVat = (double)orderTran.TranBeforeVAT;
                rc.totalVat = (double)orderTran.TransactionVAT;
                rc.totalIncVat = (double)(orderTran.TranBeforeVAT + orderTran.TransactionVAT);
                rc.receiptItems = dtOrderDetail.AsEnumerable().Select(r => new ReceiptItem
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
                    unitPriceVat = r.GetValue<double>("ProductVAT"),
                    totalDiscountIncVat = r.GetValue<double>("TotalDiscount"),
                    totalIncVat = r.GetValue<double>("NetSale"),

                }).ToList();
                rc.receiptPayments = dtPayDetail.AsEnumerable().Select(r => new ReceiptPayment
                {
                    paymentNo = r.GetValue<int>("PayDetailID"),
                    paymentCurrency = r.GetValue<string>("CurrencyCode"),
                    paymentAmount = r.GetValue<double>("PayAmount"),
                    paymentType = r.GetValue<string>("PayTypeName")
                }).ToList();

                _log.Log(NLog.LogLevel.Info, $"RequestRcCode => {JsonConvert.SerializeObject(rc)}");

                var rcCode = _rcAgent.RequestRcCode(rc);

                _log.Log(NLog.LogLevel.Info, $"Response RequestRcCode => {JsonConvert.SerializeObject(rcCode)}");
                return rcCode;
            }
        }

        private DataSet GetTransactionData(int transactionId, int computerId, MySqlConnection conn, string tableSubfix = "front")
        {
            var cmd = new MySqlCommand($@"select * from ordertransaction{tableSubfix} where TransactionID=@tid and ComputerID=@cid;
            select a.*, b.ProductCode, b.ProductName from orderdetail{tableSubfix} a left outer join products b on a.ProductID=b.ProductID where a.TransactionID=@tid and a.ComputerID=@cid and OrderStatusID=2;
            select a.*, b.PayTypeName from orderpaydetail{tableSubfix} a left outer join paytype b on a.PayTypeID=b.PayTypeID where a.TransactionID=@tid and a.ComputerID=@cid;", conn);
            cmd.Parameters.Add(new MySqlParameter("@tid", transactionId));
            cmd.Parameters.Add(new MySqlParameter("@cid", computerId));

            var ds = new DataSet();
            var adapter = new MySqlDataAdapter(cmd);
            adapter.Fill(ds);
            ds.Tables[0].TableName = "OrderTransaction";
            ds.Tables[1].TableName = "OrderDetail";
            ds.Tables[2].TableName = "OrderPayDetail";
            return ds;
        }

        public dynamic ConfirmPrintRcCode(string rcCode)
        {
            var resp = _rcAgent.ConfirmPrintRcCode(rcCode);
            return resp;
        }

        private void InitRCAgent(int shopId, int computerId)
        {
            using (var conn = _database.Connect())
            {
                var companyCode = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotCompanyCode", shopId: shopId, computerId: computerId);
                var posName = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotPosName", shopId: shopId, computerId: computerId);
                var posId = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotPosID", shopId: shopId, computerId: computerId);
                var shopCode = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotShopCode", shopId: shopId, computerId: computerId);
                var clientId = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotClientID", shopId: shopId, computerId: computerId);
                var clientSecret = _vtecPOSRepo.GetPropertyValueAsync(conn, 1153, "AotClientSecret", shopId: shopId, computerId: computerId);

                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ipAddress = host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault()?.ToString();

                _rcConfig = new RCConfig();
                _rcConfig.companyCode = companyCode.Result;
                _rcConfig.posIPAddress = "117.117.117.001";
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
