using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS
{
    public class PaymentService : IPaymentService
    {
        IDatabase _database;
        VtecPOSRepo _posRepo;
        POSModule _posModule;

        public PaymentService(IDatabase database)
        {
            _database = database;
            _posRepo = new VtecPOSRepo(database);
            _posModule = new POSModule();
        }

        public async Task AddPaymentAsync(IDbConnection conn, PaymentData paymentData)
        {
            var saleDate = await _posRepo.GetSaleDateAsync(conn, paymentData.ShopID, false);
            var dtPendingPayment = await GetPendingPaymentAsync(conn, paymentData.TransactionID, paymentData.ComputerID, paymentData.PayTypeID);
            bool isUpdate = dtPendingPayment.Rows.Count > 0;
            IDbCommand cmd = _database.CreateCommand(conn);

            cmd.CommandText = "select * from payment_currency where IsMainCurrency=1 and Activated=1 and Deleted=0";
            var dtMainCurrency = new DataTable();
            try
            {
                using (var reader = await _database.ExecuteReaderAsync(cmd))
                {
                    dtMainCurrency.Load(reader);
                }
            }
            catch { }

            if (isUpdate)
            {
                cmd.CommandText = "update orderpaydetailfront " +
                    " set CurrencyAmount = @currencyAmount, " +
                    " CashChange = @cashChange, " +
                    " CashChangeMainCurrency = @cashChangeCurrency, " +
                    " CashChangeCurrencyAmount = @cashChangeCurrency " +
                    " where TransactionID=@transactionId " +
                    " and ComputerID=@computerId " +
                    " and PayTypeID=@payTypeId";

                var pendingPaymentRow = dtPendingPayment.Rows[0];
                paymentData.PayDetailID = pendingPaymentRow.GetValue<int>("PayDetailID");
                paymentData.CurrencyAmount = paymentData.CurrencyAmount + pendingPaymentRow.GetValue<decimal>("CurrencyAmount");

                cmd.Parameters.Clear();
                cmd.Parameters.Add(_database.CreateParameter("@currencyAmount", paymentData.CurrencyAmount));
                cmd.Parameters.Add(_database.CreateParameter("@cashChange", paymentData.CashChange));
                cmd.Parameters.Add(_database.CreateParameter("@cashChangeCurrency", paymentData.CashChangeCurrencyAmount));
                cmd.Parameters.Add(_database.CreateParameter("@transactionId", paymentData.TransactionID));
                cmd.Parameters.Add(_database.CreateParameter("@computerId", paymentData.ComputerID));
                cmd.Parameters.Add(_database.CreateParameter("@payTypeId", paymentData.PayTypeID));
            }
            else
            {
                try
                {
                    if (dtMainCurrency.Rows.Count > 0)
                    {
                        paymentData.CurrencyCode = (string)dtMainCurrency.Rows[0]["CurrencyCode"];
                        paymentData.CurrencyName = (string)dtMainCurrency.Rows[0]["CurrencyName"];
                    }
                }
                catch { }

                cmd.CommandText = "insert into orderpaydetailfront " +
                    "(PayDetailID, TransactionID, ComputerID, TranKey, PayTypeID, " +
                    "PayAmount, CurrencyCode, CurrencyName, CurrencyRatio, ExchangeRate, CurrencyAmount, " +
                    "CashChange, CashChangeMainCurrency, CashChangeCurrencyAmount, CashChangeMainCurrencyCode, CashChangeCurrencyCode," +
                    "CashChangeCurrencyName, CashChangeCurrencyRatio, CashChangeExchangeRate, " +
                    "CreditCardType, BankNameID, ShopID, SaleDate, PayRemark) " +
                    "values(@payDetailId, @transactionId, @computerId, @tranKey, @payTypeId, @payAmount, " +
                    "@currencyCode, @currencyName, @currencyRatio, @exchangeRate, @currencyAmount, " +
                    "@cashChange, @cashChangeMainCurrency, @cashChangeCurrencyAmount, @cashChangeMainCurrencyCode, " +
                    "@cashChangeCurrencyCode, @cashChangeCurrencyName, @cashChangeCurrencyRatio," +
                    "@cashChangeExchangeRate, @creditCardType, @bankNameId, @shopId, @saleDate, @remark)";

                paymentData.PayDetailID = await GetMaxPayDetailIdAsync(conn, paymentData.TransactionID, paymentData.ComputerID);
                cmd.Parameters.Clear();
                cmd.Parameters.Add(_database.CreateParameter("@paydetailId", paymentData.PayDetailID));
                cmd.Parameters.Add(_database.CreateParameter("@transactionId", paymentData.TransactionID));
                cmd.Parameters.Add(_database.CreateParameter("@computerId", paymentData.ComputerID));
                cmd.Parameters.Add(_database.CreateParameter("@tranKey", paymentData.TransactionID + ":" + paymentData.ComputerID));
                cmd.Parameters.Add(_database.CreateParameter("@payTypeId", paymentData.PayTypeID));
                cmd.Parameters.Add(_database.CreateParameter("@payAmount", paymentData.PayAmount));
                cmd.Parameters.Add(_database.CreateParameter("@creditCardType", paymentData.CreditCardType));
                cmd.Parameters.Add(_database.CreateParameter("@bankNameId", paymentData.BankNameID));
                cmd.Parameters.Add(_database.CreateParameter("@currencyCode", paymentData.CurrencyCode));
                cmd.Parameters.Add(_database.CreateParameter("@currencyName", paymentData.CurrencyName));
                cmd.Parameters.Add(_database.CreateParameter("@currencyRatio", paymentData.CurrencyRatio));
                cmd.Parameters.Add(_database.CreateParameter("@exchangeRate", paymentData.ExchangeRate));
                cmd.Parameters.Add(_database.CreateParameter("@currencyAmount", paymentData.CurrencyAmount));
                cmd.Parameters.Add(_database.CreateParameter("@cashChange", paymentData.CashChange));
                cmd.Parameters.Add(_database.CreateParameter("@cashChangeCurrencyAmount", paymentData.CashChangeCurrencyAmount));
                cmd.Parameters.Add(_database.CreateParameter("@cashChangeMainCurrency", paymentData.CashChange));
                cmd.Parameters.Add(_database.CreateParameter("@cashChangeMainCurrencyCode", paymentData.CurrencyCode));
                cmd.Parameters.Add(_database.CreateParameter("@cashChangeCurrencyCode", paymentData.CurrencyCode));
                cmd.Parameters.Add(_database.CreateParameter("@cashChangeCurrencyName", paymentData.CurrencyName));
                cmd.Parameters.Add(_database.CreateParameter("@cashChangeCurrencyRatio", paymentData.CurrencyRatio));
                cmd.Parameters.Add(_database.CreateParameter("@cashChangeExchangeRate", paymentData.ExchangeRate));
                cmd.Parameters.Add(_database.CreateParameter("@shopId", paymentData.ShopID));
                cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
                cmd.Parameters.Add(_database.CreateParameter("@remark", paymentData.Remark));
            }
            await _database.ExecuteNonQueryAsync(cmd);
        }

        public async Task DeletePaymentAsync(IDbConnection conn, int transactionId, int computerId, int payDetailId)
        {
            IDbCommand cmd = _database.CreateCommand("delete from orderpaydetailfront " +
                "where " + (payDetailId > 0 ? "PayDetailID=@payDetailId " : "0=0 ") +
                "and TransactionID=@transactionId and ComputerID=@computerId", conn);
            if (payDetailId > 0)
                cmd.Parameters.Add(_database.CreateParameter("@payDetailId", payDetailId));
            cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            await _database.ExecuteNonQueryAsync(cmd);
        }

        public async Task FinalizeBillAsync(IDbConnection conn, int transactionId, int computerId, int terminalId, int shopId, int staffId)
        {

            var myConn = conn as MySqlConnection;
            string responseText = "";
            int defaultDecimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
            _posModule.OrderDetail_RefreshPromo(ref responseText, "front", transactionId, computerId, defaultDecimalDigit, myConn);
            var result = _posModule.OrderDetail_CalBill(ref responseText, transactionId, computerId, shopId, defaultDecimalDigit, "front", myConn);
            if (!string.IsNullOrEmpty(result))
                throw new VtecPOSException($"OrderDetail_CalBill {responseText}");
            var isSuccess = _posModule.OrderDetail_FinalizeBill(ref responseText, "front", transactionId, computerId, defaultDecimalDigit, staffId, terminalId, myConn);
            if (!isSuccess)
                throw new VtecPOSException($"Finalize bill {responseText}");
        }

        public async Task FinalizeOrderAsync(IDbConnection conn, int transactionId, int computerId, int terminalId, int shopId, int staffId, int langId, string printerIds, string printerNames)
        {
            var myConn = conn as MySqlConnection;
            string responseText = "";
            int defaultDecimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
            string saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, true);
            var isSuccess = _posModule.OrderDetail_Final(ref responseText, "front", transactionId, computerId, shopId, saleDate, defaultDecimalDigit, myConn);
            if (!isSuccess)
                throw new VtecPOSException($"Final Order {responseText}");
            isSuccess = _posModule.ChkMoveTranData(ref responseText, shopId, saleDate, myConn);
            if (!isSuccess)
                throw new VtecPOSException($"ChkMoveTran {responseText}");
        }

        public async Task<DataTable> GetPaymentCurrencyAsync(IDbConnection conn)
        {
            DataTable dtPaymentCurrency = new DataTable();
            IDbCommand cmd = _database.CreateCommand(@"select a.*, b.* from payment_currency a
                    inner join payment_exratetable b 
                    on a.CurrencyID=b.CurrencyID
                    where a.Deleted=@deleted and a.Activated=@activated", conn);
            cmd.Parameters.Add(_database.CreateParameter("@deleted", 0));
            cmd.Parameters.Add(_database.CreateParameter("@activated", 1));
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtPaymentCurrency.Load(reader);
            }
            return dtPaymentCurrency;
        }

        public async Task<DataSet> GetPaymentDataAsync(IDbConnection conn, int computerId, int langId, int currencyId)
        {
            DataSet ds = new DataSet();
            var cmd = _database.CreateCommand("select PayTypeList from computername where ComputerID=@computerId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            var payTypeByComp = "";
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    payTypeByComp = reader.GetValue<string>("PayTypeList");
                }
            }
            if (string.IsNullOrEmpty(payTypeByComp))
                throw new VtecPOSException($"No payment type config for computerId {computerId}");

            if (payTypeByComp.EndsWith(","))
                payTypeByComp = payTypeByComp.Substring(0, payTypeByComp.Length - 1);

            cmd = _database.CreateCommand("select PaymentAmountID, PaymentAmount, ImageName from paymentamountbutton" +
                   " where CurrencyID=@currencyId order by ButtonOrder;" +
                   " select PayTypeID, PayTypeName, EDCType from paytype" +
                   " where PayTypeID in (" + payTypeByComp + ") " +
                   " and IsOtherReceipt = 0 " +
                   " and PayTypeID > 1 " +
                   " and EDCType = 0 " +
                   " and IsAvailable = 1;" +
                   " select PayTypeGroupID, PayTypeGroupName from paytypegroup where LangID = @langId" +
                   " order by PayTypeGroupOrdering;", conn);

            cmd.Parameters.Add(_database.CreateParameter("@currencyId", currencyId));
            cmd.Parameters.Add(_database.CreateParameter("@langId", langId));

            var adapter = _database.CreateDataAdapter(cmd);
            adapter.TableMappings.Add("Table", "PaymentAmountButtons");
            adapter.TableMappings.Add("Table1", "PayTypes");
            adapter.TableMappings.Add("Table2", "PayTypeGroups");
            adapter.Fill(ds);
            return ds;
        }

        public Task<DataTable> GetPaymentDetailAsync(IDbConnection conn, int transactionId, int computerId)
        {
            IDbCommand cmd = _database.CreateCommand("select a.*, b.* FROM orderpaydetailfront a" +
                " inner join paytype b " +
                " on a.PayTypeID=b.PayTypeID " +
                " where a.TransactionID=@tranId AND a.ComputerID=@compId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@tranId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@compId", computerId));
            var adapter = _database.CreateDataAdapter(cmd);
            var ds = new DataSet();
            adapter.Fill(ds);
            return Task.FromResult(ds.Tables[0]);
        }

        public Task<DataTable> GetPendingPaymentAsync(IDbConnection conn, int transactionId, int computerId, int payTypeId)
        {
            DataSet ds = new DataSet();
            IDbCommand cmd = _database.CreateCommand(conn);
            cmd.CommandText = "select * " +
                " from orderpaydetailfront " +
                " where TransactionID=@transactionId " +
                " and ComputerID=@computerId " +
                " and PayTypeID=@payTypeId";
            cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            cmd.Parameters.Add(_database.CreateParameter("@payTypeId", payTypeId));
            var adapter = _database.CreateDataAdapter(cmd);
            adapter.Fill(ds);
            return Task.FromResult(ds.Tables[0]);
        }

        async Task<int> GetMaxPayDetailIdAsync(IDbConnection conn, int transactionId, int computerId)
        {
            int payDetailId = 0;
            IDbCommand cmd = _database.CreateCommand(
                "select case when max(PayDetailID) is null then 1 else max(PayDetailID) + 1 end as PayDetailID " +
                "from orderpaydetailfront " +
                "where TransactionID=@transactionId and ComputerID=@computerId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    payDetailId = reader.GetValue<int>("PayDetailID");
                }
            }
            return payDetailId;
        }
    }
}
