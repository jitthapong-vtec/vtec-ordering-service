using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Core
{
    public class POSWrapper
    {
        const string TableSubffix = "front";

        IDatabase _database;
        POSModule _posModule;

        public POSWrapper(IDatabase database)
        {
            _database = database;
            _posModule = new POSModule();
        }

        public async Task AddOrderAsync(IDbConnection conn, List<Order> orders)
        {
            var responseText = "";
            var decimalDigit = await GetDecimalDigitAsync(conn);
            var saleDate = await GetSaleDateAsync(conn, orders[0].ShopId, withBracket: true);

            foreach (var order in orders)
            {
                if (order.ParentProductId > 0 && order.OrderDetailLinkId == 0)
                {
                    order.OrderDetailLinkId = orders
                        .Where(o => order.ParentProductId == o.ProductId)
                        .Select(o => o.OrderDetailId).FirstOrDefault();
                }

                var orderDetailId = 0;
                var isSuccess = _posModule.OrderDetail(ref responseText, ref orderDetailId, false,
                    (int)order.SaleMode,
                    order.TransactionId, order.ComputerId, order.OrderDetailLinkId,
                    order.IndentLevel, order.ProductId, order.TotalQty,
                    order.OpenPrice, TableSubffix, decimalDigit, saleDate, order.ShopId,
                    order.IsComponentProduct, order.ParentProductId,
                    order.PGroupId, order.OtherFoodName, order.OtherProductGroupId,
                    order.OtherPrinterId, order.OtherDiscountAllow,
                    order.OtherInventoryId, order.OtherVatType, order.OtherPrintGroup,
                    order.OtherProductVatCode, order.OtherHasSc, order.OtherProductTypeId,
                    order.StaffId, order.ComputerId, order.TableId,
                    order.SetGroupNo, order.QtyRatio, conn as MySqlConnection);
                if (isSuccess)
                {
                    order.OrderDetailId = orderDetailId;
                }
                else
                {
                    throw new POSModuleException("OrderDetail", responseText);
                }
            }
        }

        public async Task AddPaymentAsync(IDbConnection conn, Payment payment)
        {
            var saleDate = await GetSaleDateAsync(conn, payment.ShopId);
            var tranIdParam = _database.CreateParameter("@transactionId", payment.TransactionId);
            var compIdParam = _database.CreateParameter("@computerId", payment.ComputerId);
            var shopIdParam = _database.CreateParameter("@shopId", payment.ShopId);
            var payTypeIdParam = _database.CreateParameter("@payTypeId", payment.PayTypeId);
            var currencyCode = "USD";
            var currencyName = "US Dollar";
            var currencyRatio = 1.0M;
            var exchangeRate = 1.0M;
            var changeExchangeRate = 1.0M;

            try
            {
                IDbCommand cmd = _database.CreateCommand(conn);
                cmd.CommandText = "select a.CurrencyCode, a.CurrencyName, " +
                    "b.CurrencyRatio, b.ExchangeRate, b.ChangeExchangeRate " +
                    "from payment_currency a " +
                    "left join payment_exratetable b " +
                    "on a.CurrencyID=b.CurrencyID where a.IsMainCurrency=1";
                using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
                {
                    if (reader.Read())
                    {
                        currencyCode = reader.GetValue<string>("CurrencyCode");
                        currencyName = reader.GetValue<string>("CurrencyName");
                        currencyRatio = reader.GetValue<decimal>("CurrencyRatio");
                        exchangeRate = reader.GetValue<decimal>("ExchangeRate");
                        changeExchangeRate = reader.GetValue<decimal>("ChangeExchangeRate");
                    }
                }

                cmd.CommandText = "select PayDetailID " +
                    " from orderpaydetailfront " +
                    " where TransactionID=@transactionId " +
                    " and ComputerID=@computerId " +
                    " and PayTypeID=@payTypeId";
                cmd.Parameters.Add(tranIdParam);
                cmd.Parameters.Add(compIdParam);
                cmd.Parameters.Add(payTypeIdParam);
                using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
                {
                    if (reader.Read())
                    {
                        payment.PaymentId = reader.GetValue<int>("PayDetailID");
                    }
                }

                decimal receiptPayPrice = 0;
                cmd.CommandText = "select ReceiptPayPrice from ordertransactionfront " +
                    "where TransactionID=@transactionId and ComputerID=@computerId";
                cmd.Parameters.Clear();
                cmd.Parameters.Add(tranIdParam);
                cmd.Parameters.Add(compIdParam);
                using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
                {
                    if (reader.Read())
                        receiptPayPrice = reader.GetValue<decimal>("ReceiptPayPrice");
                }

                bool isUpdate = payment.PaymentId > 0;
                if (isUpdate)
                {
                    cmd.CommandText = "update orderpaydetailfront " +
                        " set PayAmount = PayAmount + @payAmount, " +
                        " CurrencyAmount = PayAmount * @exchangeRate, " +
                        " CashChange = (PayAmount + @payAmount) - @receiptPayPrice, " +
                        " CashChangeMainCurrency = ((PayAmount + @payAmount) - @receiptPayPrice) * @exchangeRate " +
                        " where TransactionID=@transactionId " +
                        " and ComputerID=@computerId " +
                        " and PayTypeID=@payTypeId";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(_database.CreateParameter("@payAmount", payment.PayAmount));
                    cmd.Parameters.Add(_database.CreateParameter("@receiptPayPrice", receiptPayPrice));
                    cmd.Parameters.Add(_database.CreateParameter("@exchangeRate", exchangeRate));
                    cmd.Parameters.Add(tranIdParam);
                    cmd.Parameters.Add(compIdParam);
                    cmd.Parameters.Add(payTypeIdParam);
                }
                else
                {
                    cmd.CommandText = "select case when max(PayDetailID) is null then 1 " +
                        "else max(PayDetailID) + 1 end as PayDetailID " +
                        "from orderpaydetailfront " +
                        "where TransactionID=@transactionId and ComputerID=@computerId";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(tranIdParam);
                    cmd.Parameters.Add(compIdParam);
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            payment.PaymentId = reader.GetValue<int>("PayDetailID");
                        }
                    }

                    cmd.CommandText = "insert into orderpaydetailfront " +
                        "(PayDetailID, TransactionID, ComputerID, TranKey, PayTypeID, " +
                        "PayAmount, CurrencyCode, CurrencyName, CurrencyRatio, ExchangeRate, CurrencyAmount, " +
                        "CashChange, CashChangeMainCurrency, CashChangeMainCurrencyCode, " +
                        "CreditCardType, ShopID, SaleDate) " +
                        "values(@payDetailId, @transactionId, @computerId, @tranKey, @payTypeId, @payAmount, " +
                        "@currencyCode, @currencyName, @currencyRatio, @exchangeRate, @currencyAmount, " +
                        "@cashChange, @cashChangeMainCurrency, @cashChangeMainCurrencyCode, " +
                        "@creditCardType, @bankNameId, @shopId, @saleDate)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(tranIdParam);
                    cmd.Parameters.Add(compIdParam);
                    cmd.Parameters.Add(payTypeIdParam);
                    cmd.Parameters.Add(shopIdParam);
                    cmd.Parameters.Add(_database.CreateParameter("@paydetailId", payment.PaymentId));
                    cmd.Parameters.Add(_database.CreateParameter("@tranKey", $"{payment.TransactionId}:{payment.ComputerId}"));
                    cmd.Parameters.Add(_database.CreateParameter("@payAmount", payment.PayAmount));
                    cmd.Parameters.Add(_database.CreateParameter("@creditCardType", payment.CreditCardType));
                    cmd.Parameters.Add(_database.CreateParameter("@currencyCode", currencyCode));
                    cmd.Parameters.Add(_database.CreateParameter("@currencyName", currencyName));
                    cmd.Parameters.Add(_database.CreateParameter("@currencyRatio", currencyRatio));
                    cmd.Parameters.Add(_database.CreateParameter("@exchangeRate", exchangeRate));
                    cmd.Parameters.Add(_database.CreateParameter("@currencyAmount", payment.PayAmount * exchangeRate));
                    cmd.Parameters.Add(_database.CreateParameter("@cashChange", payment.PayAmount - receiptPayPrice));
                    cmd.Parameters.Add(_database.CreateParameter("@cashChangeMainCurrency", (payment.PayAmount - receiptPayPrice) * changeExchangeRate));
                    cmd.Parameters.Add(_database.CreateParameter("@cashChangeMainCurrencyCode", currencyCode));
                    cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
                }
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new POSModuleException("AddPayment", ex.Message, ex);
            }
        }

        public async Task CalculateBillAsync(IDbConnection conn, int shopId, int transactionId, int computerId)
        {
            var decimalDigit = await GetDecimalDigitAsync(conn);
            var responseText = "";
            _posModule.OrderDetail_CalBill(ref responseText, transactionId, computerId, shopId,
                decimalDigit, TableSubffix, conn as MySqlConnection);
            if (!string.IsNullOrEmpty(responseText))
                throw new POSModuleException("OrderDetail_CalBill", responseText);
        }

        public async Task DeletePaymentAsync(IDbConnection conn, int paymentId, int transactionId, int computerId)
        {
            var cmd = _database.CreateCommand("delete from orderpaydetailfront " +
                       "where " + (paymentId > 0 ? "PayDetailID=@payDetailId " : "0=0 ") +
                       "and TransactionID=@transactionId and ComputerID=@computerId", conn);
            if (paymentId > 0)
                cmd.Parameters.Add(_database.CreateParameter("@payDetailId", paymentId));
            cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            await _database.ExecuteNonQueryAsync(cmd);
        }

        public async Task FinalizeAsync(IDbConnection conn, int shopId, int transactionId, int computerId, int staffId, int terminalId)
        {
            var decimalDigit = await GetDecimalDigitAsync(conn);
            var saleDate = await GetSaleDateAsync(conn, shopId, withBracket: true);
            var responseText = "";
            var success = _posModule.OrderDetail_FinalizeBill(ref responseText, TableSubffix, transactionId, computerId,
                decimalDigit, staffId, terminalId, conn as MySqlConnection);
            if (!success)
                throw new POSModuleException("OrderDetail_FinalizeBill", responseText);
            success = _posModule.OrderDetail_Final(ref responseText, TableSubffix, transactionId, computerId, shopId,
                saleDate, decimalDigit, conn as MySqlConnection);
            if (!success)
                throw new POSModuleException("OrderDetail_Final", responseText);
        }

        public async Task<DataTable> GetOrderDetailAsync(IDbConnection conn, int transactionId, int computerId, int orderDetailId = 0, int langId = 0)
        {
            DataTable dtPromotion = new DataTable();
            DataTable dtBill = new DataTable();
            DataTable dtPayment = new DataTable();
            DataTable dtOrderData = new DataTable();

            await Task.Run(() =>
            {
                string responseText = "";
                bool success = _posModule.GetOrderDetail_View(ref responseText, ref dtOrderData, ref dtPromotion, ref dtBill,
                    ref dtPayment, 0, TableSubffix, transactionId, computerId, "ASC", conn as MySqlConnection);
                if (!success)
                    throw new POSModuleException("GetOrderDetail_View", responseText);
            });

            return dtOrderData;
        }

        public async Task<Transaction> OpenTransactionAsync(IDbConnection conn, Transaction transaction)
        {
            var saleDate = await GetSaleDateAsync(conn, transaction.ShopId, withBracket: true);
            var responseText = "";
            var transactionId = 0;
            var computerId = 0;
            var tranKey = "";
            var queueNo = "";
            var isSuccess = _posModule.Tran_Open(ref responseText, ref transactionId, ref computerId, ref tranKey,
               ref queueNo, TableSubffix, transaction.ShopId, saleDate, transaction.StaffId,
               transaction.ComputerId, transaction.SaleModeId,
               transaction.Name, transaction.QueueName, transaction.NoCustomer,
               transaction.TableId, (int)transaction.Status, conn as MySqlConnection);
            if (isSuccess)
            {
                transaction.TransactionId = transactionId;
                transaction.ComputerId = computerId;
            }
            else
            {
                throw new POSModuleException("Tran_Open", responseText);
            }
            return transaction;
        }

        public async Task RefreshPromoAsync(IDbConnection conn, int transactionId, int computerId)
        {
            var decimalDigit = await GetDecimalDigitAsync(conn);
            var responseText = "";
            var success = _posModule.OrderDetail_RefreshPromo(ref responseText, TableSubffix,
                transactionId, computerId, decimalDigit, conn as MySqlConnection);
            if (!success)
                throw new POSModuleException("OrderDetail_RefreshPromo", responseText);
        }

        public async Task<string> GetSaleDateAsync(IDbConnection conn, int shopId, bool chkCurrDate = false, bool withBracket = false)
        {
            string saleDate = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            MySqlConnection myConn = conn as MySqlConnection;
            MySqlCommand cmd = new MySqlCommand("select SessionDate from sessionenddaydetail " +
                "where ShopID=@shopId and IsEndDay=@isEndDay order by sessiondate desc limit 1", myConn);
            cmd.Parameters.AddWithValue("@shopId", shopId);
            cmd.Parameters.AddWithValue("@isEndDay", 0);
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    DateTime currentDate = DateTime.Now;
                    DateTime lastSaleDate = reader.GetDateTime(0);
                    var lastSaleDateEarlyNow = DateTime.Compare(lastSaleDate.Date, currentDate.Date) < 0;
                    if (chkCurrDate == false && lastSaleDateEarlyNow)
                        throw new POSModuleException("The front program did not open sale day!");

                    if (lastSaleDateEarlyNow)
                        saleDate = currentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    else
                        saleDate = lastSaleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
            }
            if (withBracket)
                saleDate = "{ d '" + saleDate + "' }";
            return saleDate;
        }

        public async Task<int> GetDecimalDigitAsync(IDbConnection conn)
        {
            int decimalDigit = 0;
            try
            {
                DataTable dtProperty = await GetProgramPropertyAsync(conn, 24);
                decimalDigit = dtProperty.Rows[0].GetValue<int>("PropertyValue");
            }
            catch (Exception) { }
            return decimalDigit;
        }

        public async Task<string> GetPropertyValueAsync(IDbConnection conn, int propertyId, string param, int shopId = 0, int computerId = 0)
        {
            var dtProp = await GetProgramPropertyAsync(conn, propertyId);
            if (dtProp.Rows.Count == 0)
                return "";
            var propRow = dtProp.Rows[0];
            if (dtProp.Rows.Count > 1)
            {
                var keyId = 0;
                var propLevel = propRow.GetValue<int>("PropertyLevelID");

                if (propLevel == 1)
                    keyId = shopId;
                else if (propLevel == 2)
                    keyId = computerId;

                var propLevelShop = dtProp.Select($"KeyID = {keyId}").FirstOrDefault();
                if (propLevelShop != null)
                    propRow = propLevelShop;
            }
            var dict = ExtractPropertyParameter(propRow.GetValue<string>("PropertyTextValue"));
            var val = dict.FirstOrDefault(x => x.Key == param).Value;
            return val;
        }

        public async Task<DataTable> GetProgramPropertyAsync(IDbConnection conn, int propertyId)
        {
            string sqlQuery = "select a.*, b.PropertyLevelID from programpropertyvalue a" +
                " left join programproperty b" +
                " on a.PropertyID=b.PropertyID" +
                " where a.PropertyID=@propertyId";
            IDbCommand cmd = _database.CreateCommand(sqlQuery, conn);
            cmd.Parameters.Add(_database.CreateParameter("@propertyId", propertyId));
            DataTable dtResult = new DataTable();
            using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        Dictionary<string, string> ExtractPropertyParameter(string propParams)
        {
            var props = propParams.Split(';').AsParallel().Select(x => x.Split('=')).ToArray();
            var dict = new Dictionary<string, string>();
            foreach (var prop in props)
            {
                try
                {
                    if (!dict.Keys.Contains(prop[0]))
                        dict.Add(prop[0], prop[1]);
                }
                catch (Exception) { }
            }
            return dict;
        }
    }
}
