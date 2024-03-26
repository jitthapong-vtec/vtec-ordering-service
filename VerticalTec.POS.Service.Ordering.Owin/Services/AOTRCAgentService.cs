using RCAgentAOTRR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using MySql.Data.MySqlClient;
using System.Data;
using System.Web.Http.Results;
using DevExpress.Data.Linq;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public class AOTRCAgentService
    {
        static readonly NLog.Logger _log = NLog.LogManager.GetLogger("logpayment");

        public string RCAgentPath => AppConfig.Instance.RCAgentPath; //@"C:\Program Files (x86)\admin\RCAgentInstaller\AIRPORTS OF THAILAND\RC Agent";

        private readonly IDatabase _database;
        private readonly VtecPOSRepo _vtecPOSRepo;
        private Assembly _assembly;
        private dynamic _rcAgent;
        private dynamic _rcConfig;

        public AOTRCAgentService(IDatabase database)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolveEventHandler);

            _database = database;
            _assembly = GetRCAgentAssembly();

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
                if (dtTrans.Rows.Count == 0)
                {
                    ds = GetTransactionData(transactionId, computerId, conn, "");
                    dtTrans = ds.Tables["OrderTransaction"];
                }

                if(dtTrans.Rows.Count == 0)
                {
                    var errMsg = $"No data of TransactionID={transactionId}, ComputerID={computerId}";
                    _log.Error(errMsg);
                    throw new Exception(errMsg);
                }

                //dynamic rc = Activator.CreateInstance(_assembly.GetType("RCAgentAOTRR.Receipt"));
                var rc = new RCAgentAOTRR.Receipt();
                rc.companyCode = _rcConfig.companyCode;
                rc.ipAddress = _rcConfig.posIPAddress;
                rc.posName = _rcConfig.posName;
                rc.rdId = _rcConfig.rdId;
                rc.shopId = _rcConfig.rdId;
                rc.transactionDatetime = DateTime.Now;
                rc.receiptDate = DateTime.Today;
                rc.receiptType = "1";
                rc.receiptStatus = "1";
                rc.taxInvoice = "1234";
                rc.refNo = "1234";

                var rcCode = _rcAgent.RequestRcCode(rc);
                return rcCode;
            }
        }

        private DataSet GetTransactionData(int transactionId, int computerId, MySqlConnection conn, string tableSubfix = "front")
        {
            var cmd = new MySqlCommand($@"select * from ordertransaction{tableSubfix} where TransactionID=@tid and ComputerID=@cid;
            select * from orderpaydetail{tableSubfix} where TransactionID=@tid and ComputerID=@cid;", conn);
            cmd.Parameters.Add(new MySqlParameter("@tid", transactionId));
            cmd.Parameters.Add(new MySqlParameter("@cid", computerId));

            var ds = new DataSet();
            var adapter = new MySqlDataAdapter(cmd);
            adapter.Fill(ds);
            ds.Tables[0].TableName = "OrderTransaction";
            ds.Tables[1].TableName = "OrderPayDetail";
            return ds;
        }

        public dynamic ConfirmPrintRcCode(string rcCode)
        {
            var resp = _rcAgent.ConfirmPrintRcCode(rcCode);
            return resp;
        }

        public Assembly GetRCAgentAssembly()
        {
            return Assembly.LoadFile(Path.Combine(RCAgentPath, "RCAgent.dll"));
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

                _rcConfig = Activator.CreateInstance(_assembly.GetType("RCAgentAOTRR.RCConfig"));
                _rcConfig.companyCode = companyCode.Result;
                _rcConfig.posIPAddress = "117.117.117.001";
                _rcConfig.posName = posName.Result;
                _rcConfig.posId = posId.Result;
                _rcConfig.shopCode = shopCode.Result;
                _rcConfig.clientId = clientId.Result;
                _rcConfig.clientSecret = clientSecret.Result;
            }

            if (_rcAgent == null)
                _rcAgent = Activator.CreateInstance(_assembly.GetType("RCAgentAOTRR.RCAgent"), _rcConfig);
        }

        private Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            return Assembly.LoadFile(Path.Combine(RCAgentPath, $"{assemblyName.Name}.dll"));
        }
    }
}
