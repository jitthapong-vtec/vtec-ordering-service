using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using vtecPOS.GlobalFunctions;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS
{
    public class VtecPOSRepo
    {
        IDatabase _database;
        POSModule _posModule;

        public VtecPOSRepo(IDatabase database)
        {
            _database = database;
            _posModule = new POSModule();
        }

        public async Task UpdateTransactionNameAsync(IDbConnection conn, int transactionId, int computerId, string transactionName)
        {
            var cmd = _database.CreateCommand("update ordertransactionfront set TransactionName=@transactionName " +
               "where TransactionID=@transactionId " +
               "and ComputerID=@computerId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@transactionName", transactionName));
            cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            await _database.ExecuteNonQueryAsync(cmd);
        }

        public async Task UpdateCustomerQtyAsync(IDbConnection conn, int transactionId, int computerId, int customerQty)
        {
            var cmd = _database.CreateCommand("update ordertransactionfront set NoCustomer=@noCustomer " +
               "where TransactionID=@transactionId " +
               "and ComputerID=@computerId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@noCustomer", customerQty));
            cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            await _database.ExecuteNonQueryAsync(cmd);
        }

        public async Task<DataSet> GetOrderDataAsync(IDbConnection conn, int transactionId, int computerId, int langId = 1)
        {
            DataSet orderDataSet = new DataSet();
            DataTable dtOrders = new DataTable();
            DataTable dtPromotion = new DataTable();
            DataTable dtBill = new DataTable();
            DataTable dtPayment = new DataTable();
            DataTable dtStock = new DataTable();
            DataTable dtOrderGroup = new DataTable();
            DataTable dtVoidItem = new DataTable();
            DataTable dtOrderData = new DataTable();

            string responseText = "";
            bool isSuccess = _posModule.GetOrderDetail_View(ref responseText,
                ref dtOrders, ref dtOrderData, ref dtPromotion, ref dtBill, ref dtPayment, ref dtOrderGroup, dtVoidItem, 1,
                "front", transactionId, computerId, "ASC", langId, conn as MySqlConnection);

            if (isSuccess)
            {
                DataTable dtOrderClone = dtOrders.Clone();
                dtOrderClone.Columns.Add("OrderStaffID", typeof(int));
                dtOrderClone.Columns.Add("SetGroupNo", typeof(int));
                dtOrderClone.Columns.Add("PGroupID", typeof(int));
                dtOrderClone.Columns.Add("QtyRatio", typeof(double));
                dtOrderClone.Columns.Add("CurrentStock", typeof(decimal));

                DataTable dtAditionOrder = await GetOrderDetailAsync(conn, transactionId, computerId);
                DataTable dtCurrentStock = await GetCurrentStockAsync(conn, 0, 0);

                var query = from order in dtOrders.ToEnumerable()
                            join order2 in dtAditionOrder.ToEnumerable()
                            on order.GetValue<int>("OrderDetailID") equals order2.GetValue<int>("OrderDetailID") into orderJoin
                            join stock in dtCurrentStock.ToEnumerable()
                            on order.GetValue<int>("ProductID") equals stock.GetValue<int>("ProductID") into stockJoin
                            from od in orderJoin.DefaultIfEmpty()
                            let ordArr = new object[]
                            {
                                od == null ? null : od["OrderStaffID"],
                                od == null ? null : od["SetGroupNo"],
                                od == null ? null : od["PGroupID"],
                                od == null ? null : od["QtyRatio"]
                            }
                            from st in stockJoin.DefaultIfEmpty()
                            let stArr = new object[]
                            {
                                st == null ? null : st["CurrentStock"]
                            }
                            select order.ItemArray.Concat(ordArr).Concat(stArr).ToArray();

                foreach (object[] rows in query)
                {
                    dtOrderClone.Rows.Add(rows);
                }

                dtOrderClone.TableName = "Orders";
                dtPromotion.TableName = "Promotions";
                dtBill.TableName = "Bill";
                dtPayment.TableName = "Payments";
                orderDataSet.Tables.Add(dtBill);
                orderDataSet.Tables.Add(dtOrderClone);
                orderDataSet.Tables.Add(dtPromotion);
                orderDataSet.Tables.Add(dtPayment);
                return orderDataSet;
            }
            else
            {
                throw new VtecPOSException(responseText);
            }
        }

        public Task<DataTable> GetOrderDetailAsync(IDbConnection conn, int transactionId, int computerId)
        {
            IDbCommand cmd = _database.CreateCommand("select * from OrderDetailfront " +
                " where TransactionID=@transactionId" +
                " and ComputerID=@computerId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@transactionId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));

            var dataSet = new DataSet();
            IDataAdapter adapter = _database.CreateDataAdapter(cmd);
            adapter.Fill(dataSet);
            adapter.TableMappings.Add("Table", "Orders");
            return Task.FromResult(dataSet.Tables[0]);
        }

        public async Task<DataTable> GetCurrentStockAsync(IDbConnection conn, int productId, int shopId)
        {
            DataTable dtCurrentStock = new DataTable();
            try
            {
                IDbCommand cmd = _database.CreateCommand("select * from productcountdownstock where 0=0", conn);
                if (productId > 0)
                {
                    cmd.CommandText += " and ProductID=@productId";
                    cmd.Parameters.Add(_database.CreateParameter("@productId", productId));
                }
                if (shopId > 0)
                {
                    cmd.CommandText += " and ShopID=@shopId";
                    cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                }
                using (var reader = await _database.ExecuteReaderAsync(cmd))
                {
                    dtCurrentStock.Load(reader);
                }
            }
            catch (Exception) { }
            return dtCurrentStock;
        }

        public async Task<DataTable> GetTableZoneAsync(IDbConnection conn, int shopId, string tableZones)
        {
            var cmd = _database.CreateCommand("select * from tablezone where ShopID=@shopId and Deleted=@deleted", conn);
            if (!string.IsNullOrEmpty(tableZones))
            {
                cmd.CommandText += " and ZoneID in (" + tableZones + ")";
            }
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_database.CreateParameter("@deleted", 0));
            DataTable dtResult = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        public async Task<DataTable> GetTableAsync(IDbConnection conn, int shopId, string zoneIds)
        {
            var saleDate = await GetSaleDateAsync(conn, shopId, false);
            var cmd = _database.CreateCommand("select a.*, case when c.TransactionID is null then 0 else c.TransactionID end as TransactionID, " +
                " case when c.ComputerID is null then 0 else c.ComputerID end as ComputerID, " +
                " case when c.NoCustomer is null then 1 else c.NoCustomer end as NoCustomer, " +
                " c.TransactionName, c.SaleMode, c.OpenTime, c.GroupNo, c.MemberID " +
                " from tableno a " +
                " inner join tablezone b " +
                " on a.ZoneID=b.ZoneID " +
                " left join(select otf.TableID, otf.GroupNo, ot.TransactionID, ot.ComputerID, ot.NoCustomer, ot.TransactionName, " +
                " ot.SaleMode, ot.OpenTime, ot.MemberID " +
                " from order_tablefront otf inner join ordertransactionfront ot " +
                " on otf.TransactionID = ot.TransactionID and otf.ComputerID = ot.ComputerID " +
                " where otf.SaleDate = @saleDate) c " +
                " on a.TableID = c.TableID" +
                " where a.Deleted=0 and b.Deleted=0 and b.ShopID=@shopId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
            if (!string.IsNullOrEmpty(zoneIds))
            {
                cmd.CommandText += " and a.ZoneID in(" + zoneIds + ")";
            }
            cmd.CommandText += " order by a.TableNumber, a.ZoneID, a.TableName";
            var dataTable = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dataTable.Load(reader);
            }

            var dtProperty = await GetProgramPropertyAsync(conn, 1075);
            var warningMinute = 0;
            var criticalMinute = 0;
            var dtBuffetSetting = new DataTable();
            if (dtProperty.Rows.Count > 0)
            {
                try
                {
                    int propValue = Convert.ToInt32(dtProperty.Rows[0]["PropertyValue"]);
                    string propTextValue = dtProperty.Rows[0]["PropertyTextValue"].ToString();
                    if (propValue == 1)
                    {
                        string[] props = propTextValue.Split(';');
                        warningMinute = Convert.ToInt32(props[0].Split('=')[1]);
                        criticalMinute = Convert.ToInt32(props[1].Split('=')[1]);
                    }
                    else if (propValue == 2)
                    {
                        dtBuffetSetting = await GetBuffetTimeSettingAsync(conn, shopId);
                    }
                }
                catch (Exception) { }
            }
            dataTable.Columns.Add("IsWarning", typeof(bool));
            dataTable.Columns.Add("IsCritical", typeof(bool));
            dataTable.Columns.Add("BuffetColorHex", typeof(string));
            dataTable.Columns.Add("TableTimeMinute", typeof(int));
            DateTime now = DateTime.Now;
            foreach (DataRow row in dataTable.Rows)
            {
                if (row["OpenTime"] != DBNull.Value)
                {
                    DateTime openTime = Convert.ToDateTime(row["OpenTime"]);
                    double totalMinute = now.Subtract(openTime).TotalMinutes;
                    row["TableTimeMinute"] = totalMinute;
                    if (warningMinute > 0 && criticalMinute > 0)
                    {
                        if (totalMinute > warningMinute)
                        {
                            row["IsWarning"] = true;
                        }
                        if (totalMinute > criticalMinute)
                        {
                            row["IsCritical"] = true;
                        }
                    }
                    else if (dtBuffetSetting.Rows.Count > 0)
                    {
                        var buffetTimes = (from settingRow in dtBuffetSetting.ToEnumerable()
                                           where totalMinute >= settingRow.GetValue<int>("TimeMinute")
                                           select settingRow).ToArray();
                        if (buffetTimes.Count() > 0)
                        {
                            row["BuffetColorHex"] = buffetTimes[buffetTimes.Count() - 1]["TimeColor"];
                        }
                        else
                        {
                            row["BuffetColorHex"] = dtBuffetSetting.Rows[0]["TimeColor"];
                        }
                    }
                }
            }
            return dataTable;
        }

        public async Task<bool> GetOpenedTableTransactionAsync(IDbConnection conn, Transaction transaction)
        {
            IDbCommand cmd = _database.CreateCommand(
                   "select * from order_tablefront " +
                   "where TableID=@tableId and ShopID=@shopId and SaleDate=@saleDate", conn);
            cmd.Parameters.Add(_database.CreateParameter("@tableId", transaction.TableID));
            cmd.Parameters.Add(_database.CreateParameter("@shopId", transaction.ShopID));
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", await GetSaleDateAsync(conn, transaction.ShopID, false, true)));
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    var transactionId = reader.GetValue<int>("TransactionID");
                    var computerId = reader.GetValue<int>("ComputerID");
                    transaction.TransactionID = transactionId;
                    transaction.ComputerID = computerId;
                    if (transactionId > 0 && computerId > 0)
                        return true;
                }
            }
            return false;
        }

        public async Task<DataSet> GetQuestionAsync(IDbConnection conn, int shopId, int transactionId, int computerId)
        {
            var cmd = _database.CreateCommand("select a.*, b.*, c.* " +
                " from questiondefinedata a" +
                " left join questiondefinedatagroup b" +
                " on a.QDDGID=b.QDDGID " +
                " left join questiondefineoption c" +
                " on a.QDDID = c.QDDID " +
                " join questionshoplink d" +
                " on a.QDDID = d.QDDID " +
                " where a.Deleted = 0" +
                " and a.Activated = 1" +
                " and d.ShopID=@shopId" +
                " order by b.QDDGOrder, a.QDDOrdering;" +
                " select a.QDDID, a.OptionID, a.QDVValue from questiondefinedetailfront a" +
                " inner join ordertransactionfront b" +
                " on a.TransactionID=b.TransactionID and a.ComputerID=b.ComputerID" +
                " where a.TransactionID=@tranId and a.ComputerID=@compId and a.SaleDate=@saleDate and b.TransactionStatusID=1;", conn);

            var saleDate = await GetSaleDateAsync(conn, shopId, false);
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
            cmd.Parameters.Add(_database.CreateParameter("@tranId", transactionId));
            cmd.Parameters.Add(_database.CreateParameter("@compId", computerId));
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));

            var ds = new DataSet();
            var adapter = _database.CreateDataAdapter(cmd);
            adapter.TableMappings.Add("Table", "Question");
            adapter.TableMappings.Add("Table1", "QuestionTransaction");
            adapter.Fill(ds);
            return ds;
        }

        public async Task AddQuestionAsync(IDbConnection conn, OrderTransaction tranData)
        {
            if (tranData.Questions == null || tranData.Questions.Count() == 0)
                return;
            try
            {
                string saleDate = await GetSaleDateAsync(conn, tranData.ShopID, false);
                string sqlDelete = "delete from questiondefinedetailfront" +
                    " where TransactionID=@tranId and ComputerID=@compId and SaleDate=@saleDate";
                IDbCommand cmd = _database.CreateCommand(sqlDelete, conn);
                cmd.Parameters.Add(_database.CreateParameter("@tranId", tranData.TransactionID));
                cmd.Parameters.Add(_database.CreateParameter("@compId", tranData.ComputerID));
                cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
                await _database.ExecuteNonQueryAsync(cmd);

                string sqlInsert = "insert into questiondefinedetailfront(TransactionID, ComputerID, TranKey, ShopID, SaleDate, QDDID, OptionID, QDVText, QDVValue) values";
                for (int i = 0; i < tranData.Questions.Count; i++)
                {
                    QuestionOption question = tranData.Questions[i];
                    sqlInsert += "(" + tranData.TransactionID + ", " + tranData.ComputerID + ", '" +
                        tranData.TransactionID + ":" + tranData.ComputerID + "', " +
                        tranData.ShopID + ", '" + saleDate + "', " + question.QuestionID + ", " +
                        question.OptionID + ", '" + question.QuestionText + "', " + question.QuestionValue + ")";
                    if (i < tranData.Questions.Count - 1)
                        sqlInsert += ",";
                }
                cmd = _database.CreateCommand(sqlInsert, conn);
                await _database.ExecuteNonQueryAsync(cmd);
            }
            catch (Exception ex)
            {
                throw new VtecPOSException("An error occurred when AddQuestion " + ex.Message, ex);
            }
        }

        public async Task InsertUpdateMemberAsync(IDbConnection conn, MemberData member)
        {
            var alreadyExists = false;
            var cmd = _database.CreateCommand("select count(MemberID) from Members WHERE MemberID=@memberId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@memberId", member.MemberId));
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                if (reader.Read() && reader.GetInt32(0) > 0)
                    alreadyExists = true;
            }

            if (!alreadyExists)
            {
                var shopData = await GetShopDataAsync(conn);
                cmd.CommandText = "INSERT INTO Members (MemberID, MemberGroupID, MemberCode,  MemberFirstName, MemberLastName, " +
                        "MemberAddress1, MemberAddress2, MemberCity, MemberZipCode, " +
                        "InputBy, InputDate, UpdateBy, UpdateDate, InsertAtShopID, Activated, Deleted) " +
                        "VALUES (@memberId, @memberGroupId, @memberCode, @firstName, @lastName, @addr1, " +
                        "@addr2, @city, @zipCode, @inputBy, @inputDate, @inputBy, @updateDate, @shopId, 1, 0)";
                cmd.Parameters.Clear();
                cmd.Parameters.Add(_database.CreateParameter("@memberId", member.MemberId));
                cmd.Parameters.Add(_database.CreateParameter("@memberGroupId", member.MemberGroupId));
                cmd.Parameters.Add(_database.CreateParameter("@memberCode", member.MemberCode));
                cmd.Parameters.Add(_database.CreateParameter("@firstName", member.FirstName));
                cmd.Parameters.Add(_database.CreateParameter("@lastName", member.LastName));
                cmd.Parameters.Add(_database.CreateParameter("@addr1", member.Address1));
                cmd.Parameters.Add(_database.CreateParameter("@addr2", member.Address2));
                cmd.Parameters.Add(_database.CreateParameter("@city", member.City));
                cmd.Parameters.Add(_database.CreateParameter("@zipCode", member.ZipCode));
                cmd.Parameters.Add(_database.CreateParameter("@inputBy", 2));
                cmd.Parameters.Add(_database.CreateParameter("@inputDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
                cmd.Parameters.Add(_database.CreateParameter("@updateDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
                cmd.Parameters.Add(_database.CreateParameter("@shopId", shopData.Rows[0].GetValue<int>("ShopID")));
                await _database.ExecuteNonQueryAsync(cmd);
            }

            var alreadyExistsMemberCard = false;
            cmd.CommandText = "select count(MemberID) from Member_Card WHERE MemberID=@memberId";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(_database.CreateParameter("@memberId", member.MemberId));
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                if (reader.Read() && reader.GetInt32(0) > 0)
                    alreadyExistsMemberCard = true;
            }
            if (!alreadyExistsMemberCard)
            {
                cmd.CommandText = "INSERT INTO Member_Card (CardID, CardTypeID, MemberID, CardNumber, CardPoint, CardBalance, CardStatus) " +
                            "VALUES (@cardId, 1, @cardId, '', 0, 0, 1)";
                cmd.Parameters.Clear();
                cmd.Parameters.Add(_database.CreateParameter("@cardId", member.MemberId));
                await _database.ExecuteNonQueryAsync(cmd);
            }
        }

        public async Task SetComputerAccessAsync(IDbConnection conn, int tableId, int accessComputerId)
        {
            if (tableId == 0)
                return;
            try
            {
                var cmd = _database.CreateCommand("update tableno set CurrentAccessComputer=@accessComputerID where TableID=@tableId", conn);
                cmd.Parameters.Add(_database.CreateParameter("@accessComputerID", accessComputerId));
                cmd.Parameters.Add(_database.CreateParameter("@tableId", tableId));
                await _database.ExecuteNonQueryAsync(cmd);
            }
            catch (Exception) { }
        }

        public async Task<DataTable> GetProductModifierAsync(IDbConnection conn, int shopId, string productCode, SaleModes saleMode)
        {
            DataTable dtComment = await GetProductsAsync(conn, shopId, 0, 0, "", "14,15,16", saleMode);
            if (!string.IsNullOrEmpty(productCode))
            {
                DataTable dtFixComment = new DataTable();
                var cmd = _database.CreateCommand("select * from productfixcomment where ProductCode=@productCode", conn);
                cmd.Parameters.Add(_database.CreateParameter("@productCode", productCode));
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    dtFixComment.Load(reader);
                }

                if (dtFixComment.Rows.Count > 0)
                {
                    try
                    {
                        var fixCommentQuery = (from fixComment in dtFixComment.ToEnumerable()
                                               join comment in dtComment.ToEnumerable()
                                               on fixComment.GetValue<string>("CommentCode") equals comment.GetValue<string>("ProductCode")
                                               select comment);
                        if (fixCommentQuery.Count() > 0)
                        {
                            var dtClone = dtComment.Clone();
                            foreach (var fixComment in fixCommentQuery)
                            {
                                dtClone.ImportRow(fixComment);
                            }
                            dtComment = dtClone;
                        }
                    }
                    catch (Exception) { }
                }
                else
                {
                    if (!string.IsNullOrEmpty(productCode))
                        dtComment.Clear();
                }
            }
            return dtComment;
        }

        public async Task<DataTable> GetComputerProductAsync(IDbConnection conn, int computerId)
        {
            IDbCommand cmd = _database.CreateCommand("select * from computerproduct where ComputerID=@computerId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            DataTable dtComputerProduct = new DataTable();
            try
            {
                using (var reader = await _database.ExecuteReaderAsync(cmd))
                {
                    dtComputerProduct.Load(reader);
                }
            }
            catch (Exception) { }
            return dtComputerProduct;
        }

        public async Task<DataTable> GetStaffAsync(IDbConnection conn, string staffLogin, string password)
        {
            var loginType = 0;
            var dtProperty = await GetProgramPropertyAsync(conn, 1055);
            if (dtProperty.Rows.Count > 0)
            {
                loginType = dtProperty.Rows[0].GetValue<int>("PropertyValue");
            }
            string sqlQuery = "select a.*, b.LangCode, b.LangName, b.LangCultureString" +
                " from staffs a" +
                " left join language b" +
                " on a.LangID=b.LangID" +
                " where a.Deleted = 0 and a.Activated=1";
            var cmd = _database.CreateCommand(conn);
            if (loginType == 0 || loginType == 1)
            {
                if (loginType == 0)
                {
                    sqlQuery += " and a.StaffLogin=@staffLogin and a.StaffPassword=UPPER(SHA1(@staffPassword))";
                    cmd.Parameters.Add(_database.CreateParameter("@staffLogin", staffLogin));
                    cmd.Parameters.Add(_database.CreateParameter("@staffPassword", password));
                }
                else
                {
                    sqlQuery += " and a.StaffLogin=@staffLogin";
                    cmd.Parameters.Add(_database.CreateParameter("@staffLogin", staffLogin));
                }
            }
            else if (loginType == 2)
            {

                sqlQuery += " and a.StaffPassword=UPPER(SHA1(@staffPassword))";
                cmd.Parameters.Add(_database.CreateParameter("@staffPassword", password));
            }
            cmd.CommandText = sqlQuery;
            var adapter = _database.CreateDataAdapter(cmd);
            var dataset = new DataSet();
            adapter.Fill(dataset);
            return dataset.Tables[0];
        }

        public async Task<bool> CheckStaffAccessShop(IDbConnection conn, int staffId, int shopId)
        {
            var allowAccess = false;
            var cmd = _database.CreateCommand("select count(StaffID) from staffaccessshop where StaffID=@staffId and ShopID=@shopId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@staffId", staffId));
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                if (reader.Read() && reader.GetInt32(0) > 0)
                    allowAccess = true;
            }
            return allowAccess;
        }

        public async Task<DataTable> GetStaffPermissionAsync(IDbConnection conn, int staffRoleId = 0)
        {
            MySqlConnection myConn = conn as MySqlConnection;
            var cmd = _database.CreateCommand("select * from staffpermission where true", conn);
            if (staffRoleId > 0)
            {
                cmd.CommandText += " and StaffRoleID=@staffRoleId";
                cmd.Parameters.Add(_database.CreateParameter("@staffRoleId", staffRoleId));
            }
            DataTable dataTable = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dataTable.Load(reader);
            }
            return dataTable;
        }

        public async Task<DataTable> GetProductGroupsAsync(IDbConnection conn, string productGroupIds)
        {
            string sqlQuery = "select *" +
                " from productgroup " +
                " where IsComment=@isComment" +
                " and ProductGroupActivate=@activate" +
                " and DisplayMobile=@displayMobile" +
                " and Deleted=@deleted";

            var cmd = _database.CreateCommand(conn);
            cmd.Parameters.Add(_database.CreateParameter("@isComment", 0));
            cmd.Parameters.Add(_database.CreateParameter("@activate", 1));
            cmd.Parameters.Add(_database.CreateParameter("@displayMobile", 1));
            cmd.Parameters.Add(_database.CreateParameter("@deleted", 0));

            if (!string.IsNullOrEmpty(productGroupIds))
            {
                sqlQuery += " and ProductGroupID in (" + productGroupIds + ")";
            }
            sqlQuery += " order by ProductGroupOrdering, ProductGroupName, ProductGroupCode";
            cmd.CommandText = sqlQuery;

            DataTable dtResult = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        public async Task<DataTable> GetProductDeptsAsync(IDbConnection conn, int productGroupId, string productDeptIds)
        {
            string sqlQuery = "select *" +
                " from productdept " +
                " where ProductDeptActivate=@activate" +
                " and DisplayMobile=@displayMobile" +
                " and Deleted=@deleted";

            var cmd = _database.CreateCommand(conn);
            cmd.Parameters.Add(_database.CreateParameter("@activate", 1));
            cmd.Parameters.Add(_database.CreateParameter("@displayMobile", 1));
            cmd.Parameters.Add(_database.CreateParameter("@deleted", 0));

            if (productGroupId > 0)
            {
                sqlQuery += " and ProductGroupID=@productGroupId";
                cmd.Parameters.Add(_database.CreateParameter("@productGroupId", productGroupId));
            }
            if (!string.IsNullOrEmpty(productDeptIds))
            {
                sqlQuery += " and ProductDeptID in (" + productDeptIds + ")";
            }
            sqlQuery += " order by ProductDeptOrdering, ProductDeptName, ProductDeptCode";
            cmd.CommandText = sqlQuery;

            DataTable dtResult = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        public async Task<DataTable> GetProductsAsync(IDbConnection conn, int shopId, int productGroupId, int productDeptId, string productIds, SaleModes saleMode)
        {
            return await GetProductsAsync(conn, shopId, productGroupId, productDeptId, productIds, "0,1,2,5,7,14,15", saleMode);
        }

        public async Task<DataTable> GetProductsAsync(IDbConnection conn, int shopId, int productGroupId, int productDeptId, string productIds, string productType, SaleModes saleMode)
        {
            string sqlQuery = "select a.ProductID, a.ProductGroupID, a.ProductDeptID," +
                " a.ProductTypeID, a.ProductCode, a.ProductName, a.ProductName1, a.ProductName2, a.ProductName3, a.DisplayMobile," +
                " case when b.ProductPrice is not null then b.ProductPrice else" +
                " case when c.ProductPrice is not null then c.ProductPrice else null end end as ProductPrice, " +
                " 0 as TotalQty, d.CurrentStock, a.ProductPictureServer, RequireAddAmount" +
                " from products a" +
                " left join" +
                " (select ProductID, ProductPrice from productprice where FromDate <= @saleDate and ToDate >= @saleDate and SaleMode=@saleMode) b" +
                " on a.ProductID = b.ProductID" +
                " left join" +
                " (select ProductID, ProductPrice from productprice where FromDate <= @saleDate and ToDate >= @saleDate and SaleMode=@dfSaleMode) c" +
                " on a.ProductID = c.ProductID" +
                " left join productcountdownstock d" +
                " on a.ProductID=d.ProductID" +
                " and a.ShopID=d.ShopID" +
                " where a.ProductTypeID in (" + productType + ")" +
                " and a.Deleted = @deleted" +
                " and a.ProductActivate = @activate";

            var cmd = _database.CreateCommand(conn);

            DataTable dtDefaultSaleMode = await GetDefaultSaleModeAsync(conn);

            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_database.CreateParameter("@saleMode", saleMode));
            cmd.Parameters.Add(_database.CreateParameter("@dfSaleMode", dtDefaultSaleMode.Rows[0]["SaleModeID"]));
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            cmd.Parameters.Add(_database.CreateParameter("@activate", 1));
            cmd.Parameters.Add(_database.CreateParameter("@deleted", 0));

            if (productGroupId > 0)
            {
                sqlQuery += " and a.ProductGroupID = @productGroupId";
                cmd.Parameters.Add(_database.CreateParameter("@productGroupId", productGroupId));
            }
            if (productDeptId > 0)
            {
                sqlQuery += " and a.ProductDeptID = @productDeptId";
                cmd.Parameters.Add(_database.CreateParameter("@productDeptId", productDeptId));
            }
            if (!string.IsNullOrEmpty(productIds))
            {
                sqlQuery += " and a.ProductID in (" + productIds + ")";
            }
            if (saleMode == SaleModes.DineIn)
            {
                sqlQuery += " and a.SaleMode1=1";
            }
            else if (saleMode == SaleModes.TakeAway)
            {
                sqlQuery += " and a.SaleMode2=1";
            }
            sqlQuery += " order by a.ProductOrdering, a.ProductName, a.ProductCode";
            cmd.CommandText = sqlQuery;

            DataTable dtResult = new DataTable();
            using (IDataReader reader = cmd.ExecuteReader())
            {
                dtResult.Load(reader);
            }
            //await SetSaleModePrefixTextAsync(conn, saleMode, dtResult);
            return dtResult;
        }

        public async Task<DataTable> GetProductComponentAsync(IDbConnection conn, int shopId, int pgroupId, int parentProductId, SaleModes saleMode = SaleModes.DineIn)
        {
            var rootDir = await GetPropertyValueAsync(conn, 1012, "RootWebDir", shopId);
            var backOfficePath = await GetPropertyValueAsync(conn, 1012, "BackOfficePath", shopId);
            var imageUrlBase = $"{rootDir}/{backOfficePath}/UploadImage/Products/";

            string sqlQuery = "select a.PGroupID, a.MaterialAmount, a.QtyRatio, " +
                " a.SaleMode, b.ProductID, b.ProductCode, b.ProductName, " +
                " b.ProductName1, b.ProductName2, b.ProductName3, b.ProductTypeID," +
                " case when c.ProductPrice is not null then c.ProductPrice else" +
                " case when d.ProductPrice is not null then d.ProductPrice else null end end as ProductPrice, " +
                " concat(@imageUrlBase, replace(case when b.ProductPictureServer is null then '' else b.ProductPictureServer end, 'UploadImage/Products/', '')) as ProductImage" +
                " from productcomponent a " +
                " inner join products b" +
                " on a.MaterialID=b.ProductID" +
                " left join " +
                " (select ProductID, ProductPrice from productprice where FromDate <= @saleDate and ToDate >= @saleDate and SaleMode=@saleMode) c" +
                " on b.ProductID = c.ProductID" +
                " left join" +
                " (select ProductID, ProductPrice from productprice where FromDate <= @saleDate and ToDate >= @saleDate and SaleMode=@dfSaleMode) d" +
                " on b.ProductID = d.ProductID" +
                " where a.ProductID=@parentProductId and b.Deleted=0";

            var saleDate = await GetSaleDateAsync(conn, shopId, false, true);
            var dtDefaultSaleMode = await GetDefaultSaleModeAsync(conn);
            var cmd = _database.CreateCommand(conn);
            cmd.Parameters.Add(_database.CreateParameter("@parentProductId", parentProductId));
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
            cmd.Parameters.Add(_database.CreateParameter("@saleMode", (int)saleMode));
            cmd.Parameters.Add(_database.CreateParameter("@dfSaleMode", dtDefaultSaleMode.Rows[0]["SaleModeID"]));
            cmd.Parameters.Add(_database.CreateParameter("@imageUrlBase", imageUrlBase));

            if (pgroupId > 0)
            {
                sqlQuery += " and a.PGroupID=@pgroupId";
                cmd.Parameters.Add(_database.CreateParameter("@pgroupId", pgroupId));
            }
            sqlQuery += " order by a.Ordering";

            cmd.CommandText = sqlQuery;

            DataTable dtResult = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        public async Task<DataTable> GetProductComponentGroupAsync(IDbConnection conn, int parentProductId)
        {
            string sqlQuery = "select a.*, b.ProductCode, b.ProductName" +
                " from productcomponentgroup a " +
                " left join products b " +
                " on a.ProductID=b.ProductID" +
                " where a.ProductID=@parentProductId" +
                " and b.Deleted=0";
            var cmd = _database.CreateCommand(sqlQuery, conn);
            cmd.Parameters.Add(_database.CreateParameter("@parentProductId", parentProductId));

            DataTable dtResult = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        public async Task<DataTable> GetProductCountdownStockAsync(IDbConnection conn, int shopId)
        {
            string sqlQuery = "select a.*, b.ProductName" +
                " from productcountdownstock a" +
                " left join products b" +
                " on a.ProductID=b.ProductID" +
                " and a.ShopID=b.ShopID" +
                " where a.ShopID=@shopId";
            var cmd = _database.CreateCommand(sqlQuery, conn);
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            DataTable dtCountdownStock = new DataTable();
            using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtCountdownStock.Load(reader);
            }
            return dtCountdownStock;
        }

        public Task<DataTable> GetFavoritePageIndexAsync(IDbConnection conn, int shopId, int pageType = -1)
        {
            var cmd = _database.CreateCommand(conn);
            cmd.CommandText = "select * from favoritepageindex where ShopID=@shopId";
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            if (pageType > -1)
            {
                cmd.CommandText += " and PageType=@pageType";
                cmd.Parameters.Add(_database.CreateParameter("@pageType", pageType));
            }
            cmd.CommandText += " order by PageOrder";
            DataSet dataSet = new DataSet();
            IDataAdapter adapter = _database.CreateDataAdapter(cmd);
            adapter.Fill(dataSet);
            var dataTable = dataSet.Tables[0];
            dataTable.TableName = "FavoritePageIndex";
            return Task.FromResult(dataTable);
        }

        public async Task<DataTable> GetFavoritProductsAsync(IDbConnection conn, int shopId, int pageType = -1, int pageIndex = 0, SaleModes saleMode = SaleModes.DineIn)
        {
            var cmd = _database.CreateCommand(conn);
            cmd.CommandText = "select a.*, b.ProductID, b.ProductGroupID, b.ProductDeptID," +
                " b.ProductTypeID, b.ProductCode, b.ProductName, b.ProductName1, b.ProductName2, b.ProductName3, " +
                " case when c.ProductPrice is not null then c.ProductPrice else" +
                " case when d.ProductPrice is not null then d.ProductPrice else null end end as ProductPrice," +
                " e.CurrentStock, b.ProductPictureServer" +
                " from favoriteproducts a" +
                " left join products b" +
                " on a.ProductID = b.ProductID" +
                " and a.ShopID = b.ShopID" +
                " left join (select ProductID, ProductPrice from productprice where FromDate <= @saleDate and ToDate >= @saleDate and SaleMode=@saleMode) c" +
                " on b.ProductID = c.ProductID" +
                " left join" +
                " (select ProductID, ProductPrice from productprice where FromDate <= @saleDate and ToDate >= @saleDate and SaleMode=@dfSaleMode) d" +
                " on b.ProductID = d.ProductID" +
                " left join productcountdownstock e" +
                " on b.ProductID=e.ProductID" +
                " and b.ShopID=e.ShopID" +
                " where b.ShopID=@shopId" +
                " and b.ProductTypeID in (0,1,2,7)" +
                " and b.Deleted = 0";

            if (pageType > -1)
            {
                cmd.CommandText += " and a.PageType=@pageType";
                cmd.Parameters.Add(_database.CreateParameter("@pageType", pageType));
            }
            if (pageIndex > 0)
            {
                cmd.CommandText += " and a.PageIndex=@pageIndex";
                cmd.Parameters.Add(_database.CreateParameter("@pageIndex", pageIndex));
            }
            cmd.CommandText += " order by a.ButtonOrder, b.ProductOrdering, b.ProductName, b.ProductCode";

            string saleDate = await GetSaleDateAsync(conn, shopId, false, true);
            DataTable dtDefaultSaleMode = await GetDefaultSaleModeAsync(conn);
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
            cmd.Parameters.Add(_database.CreateParameter("@saleMode", saleMode));
            cmd.Parameters.Add(_database.CreateParameter("@dfSaleMode", dtDefaultSaleMode.Rows[0]["SaleModeID"]));

            var dataSet = new DataSet();
            var dataAdapter = _database.CreateDataAdapter(cmd);
            dataAdapter.Fill(dataSet);

            var dataTable = dataSet.Tables[0];
            dataTable.TableName = "FavoriteProduct";
            await SetSaleModePrefixTextAsync(conn, saleMode, dataTable);
            return dataTable;
        }

        public Task<DataTable> GetDefaultSaleModeAsync(IDbConnection conn)
        {
            string sqlQuery = "select * from salemode where Deleted=0 and IsDefault=1";
            var cmd = _database.CreateCommand(sqlQuery, conn);
            var adapter = _database.CreateDataAdapter(cmd);
            var dataSet = new DataSet();
            adapter.Fill(dataSet);
            return Task.FromResult(dataSet.Tables[0]);
        }

        public Task<DataTable> GetSaleModeAsync(IDbConnection conn, string saleModeIds)
        {
            string sqlQuery = " select * from salemode where Deleted=0 and SaleModeID in (" + saleModeIds + ")";
            var cmd = _database.CreateCommand(sqlQuery, conn);
            var adapter = _database.CreateDataAdapter(cmd);
            DataSet dataSet = new DataSet();
            adapter.Fill(dataSet);
            return Task.FromResult(dataSet.Tables[0]);
        }

        public async Task<string> GetSaleDateAsync(IDbConnection conn, int shopId, bool includeSpecialSyntax, bool ignoreOpenDayCheck = false)
        {
            var currentDate = DateTime.Today;
            var saleDate = currentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var cmd = _database.CreateCommand("SELECT DATE_FORMAT(NOW(),'%Y-%m-%d') as CurrentDate;", conn);
            using(var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    currentDate = reader.GetValue<DateTime>("CurrentDate");
                }
            }

            cmd = _database.CreateCommand("select SessionDate from sessionenddaydetail " +
                "where ShopID=@shopId and IsEndDay=@isEndDay order by sessiondate desc limit 1", conn);
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_database.CreateParameter("@isEndDay", 0));
            try
            {
                using (var reader = await _database.ExecuteReaderAsync(cmd))
                {
                    if (reader.Read())
                    {
                        DateTime lastSaleDate = reader.GetDateTime(0);
                        var lastSaleDateEarlyNow = DateTime.Compare(lastSaleDate.Date, currentDate.Date) < 0;
                        if (ignoreOpenDayCheck == false && lastSaleDateEarlyNow)
                            throw new VtecPOSException("Store is not open!");

                        // incase bypass check open day must use last date
                        if (lastSaleDateEarlyNow)
                            saleDate = currentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        else
                            saleDate = lastSaleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new VtecPOSException(ex.Message, ex);
            }
            if (includeSpecialSyntax)
                saleDate = "{ d '" + saleDate + "' }";
            return saleDate;
        }

        public async Task<int> GetDefaultDecimalDigitAsync(IDbConnection conn)
        {
            int decimalDigit = 0;
            try
            {
                DataTable dtProperty = await GetProgramPropertyAsync(conn, 24);
                decimalDigit = Convert.ToInt32(dtProperty.Rows[0]["PropertyValue"]);
            }
            catch (Exception) { }
            return decimalDigit;
        }

        public async Task<int> GetCashierComputerIdAsync(IDbConnection conn, int shopId)
        {
            try
            {
                string sqlQuery = "select ComputerID from session where SessionDate=@saleDate" +
                    " and ShopID=@shopId and CloseStaffID=0 Order by ComputerID";
                var saleDate = await GetSaleDateAsync(conn, shopId, false);
                var cmd = _database.CreateCommand(sqlQuery, conn);
                cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));

                int computerId = 0;
                using (var reader = await _database.ExecuteReaderAsync(cmd))
                {
                    if (reader.Read())
                        computerId = reader.GetValue<int>("ComputerID");
                }
                return computerId;
            }
            catch (Exception ex)
            {
                throw new VtecPOSException(ex.Message, ex);
            }
        }

        public async Task<DataTable> GetComputerAsync(IDbConnection conn, int computerId)
        {
            string sqlQuery = "select * from computername where ComputerID=@computerId";
            var cmd = _database.CreateCommand(sqlQuery, conn);
            cmd.Parameters.Add(_database.CreateParameter("@computerId", computerId));
            DataTable dtResult = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        public Task<DataTable> GetShopDataAsync(IDbConnection conn)
        {
            string sqlQuery = "select ShopID, ShopCode, ShopName, BrandID, MerchantID" +
                " from shop_data where IsShop=1 and Deleted=0";
            var cmd = _database.CreateCommand(sqlQuery, conn);
            var adapter = _database.CreateDataAdapter(cmd);
            var dataSet = new DataSet();
            adapter.Fill(dataSet);
            return Task.FromResult(dataSet.Tables[0]);
        }

        public async Task<ShopTypes> GetShopTypeAsync(IDbConnection conn, int shopId)
        {
            ShopTypes shopType = ShopTypes.RestaurantTable;
            try
            {
                string sqlQuery = "select ShopTypeID from shop_data where shopId=@shopId";
                var cmd = _database.CreateCommand(sqlQuery, conn);
                cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
                {
                    if (reader.Read())
                    {
                        shopType = (ShopTypes)reader.GetValue<int>("ShopTypeID");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new VtecPOSException($"An error occurred when GetShopType {ex.Message}");
            }
            return shopType;
        }

        public async Task<DataTable> GetBuffetTimeSettingAsync(IDbConnection conn, int shopId)
        {
            var saleDate = await GetSaleDateAsync(conn, shopId, false);
            var cmd = _database.CreateCommand("select b.BuffetTimeTypeHeader, c.*" +
                " from buffet_timetypesetting a" +
                " inner join buffet_timetypeheader b " +
                " on a.BuffetTimeTypeID = b.BuffetTimeTypeID " +
                " and a.ShopID = b.ShopID " +
                " inner join buffet_timetypedetail c" +
                " on b.BuffetTimeTypeID = c.BuffetTimeTypeID" +
                " and b.ShopID = c.ShopID " +
                " where a.SaleDate = @saleDate AND a.ShopID = @shopId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            DataTable dtResult = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        public async Task<string> GetLoyaltyApiAsync(IDbConnection conn)
        {
            var baseUrl = await GetPropertyValueAsync(conn, 1013, "LoyaltyWebServiceUrl");
            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";
            return baseUrl;
        }

        public async Task<string> GetKioskMenuImageBaseUrlAsync(IDbConnection conn, int shopId)
        {
            var baseImageUrl = await GetBackofficeHQPathAsync(conn, shopId);
            return $"{baseImageUrl}UploadImage/Kiosk/Products/";
        }

        public async Task<string> GetKioskAdsImageBaseUrlAsync(IDbConnection conn, int shopId)
        {
            var baseImageUrl = await GetBackofficeHQPathAsync(conn, shopId);
            return $"{baseImageUrl}UploadImage/Kiosk/Ads/";
        }

        public async Task<string> GetBackofficeHQPathAsync(IDbConnection conn, int shopId)
        {
            var rootDir = await GetPropertyValueAsync(conn, 1012, "RootWebDir", shopId);
            var backoffice = await GetPropertyValueAsync(conn, 1012, "BackOfficePath", shopId);
            return $"{rootDir}/{backoffice}/";
        }

        public async Task<string> GetResourceUrl(IDbConnection conn, int shopId)
        {
            var fileResourceUrl = await GetPropertyValueAsync(conn, 1130, "FileResourceUrl", shopId);
            if (!string.IsNullOrEmpty(fileResourceUrl) && !fileResourceUrl.EndsWith("/"))
                fileResourceUrl += "/";
            return fileResourceUrl;
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

                var propLevelShop = dtProp.ToEnumerable().Where(row => row.GetValue<int>("KeyID") == keyId).FirstOrDefault();
                if (propLevelShop != null)
                    propRow = propLevelShop;
            }
            var dict = ExtractPropertyParameter(propRow.GetValue<string>("PropertyTextValue"));
            var val = dict.FirstOrDefault(x => x.Key == param).Value;
            return val;
        }

        public async Task<DataTable> GetProgramPropertyAsync(IDbConnection conn, int propertyId = 0)
        {
            IDbCommand cmd = _database.CreateCommand("select a.*, b.PropertyLevelID from programpropertyvalue a" +
                " left join programproperty b" +
                " on a.PropertyID=b.PropertyID" +
                " where b.Deleted=0", conn);
            if (propertyId > 0)
            {
                cmd.CommandText += " and a.PropertyID = @propertyId";
                cmd.Parameters.Add(_database.CreateParameter("@propertyId", propertyId));
            }
            DataTable dtResult = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        async Task SetSaleModePrefixTextAsync(IDbConnection conn, SaleModes saleMode, DataTable dataTable)
        {
            if (dataTable.Rows.Count > 0)
            {
                DataTable dtSaleMode = await GetSaleModeAsync(conn, ((int)saleMode).ToString());
                DataRow sRow = dtSaleMode.Rows[0];
                string prefixText = sRow.GetValue<string>("PrefixText");
                int position = sRow.GetValue<int>("PositionPrefix");
                foreach (DataRow row in dataTable.Rows)
                {
                    var name = row.GetValue<string>("ProductName");
                    var name1 = row.GetValue<string>("ProductName1");
                    var name2 = row.GetValue<string>("ProductName2");
                    var name3 = row.GetValue<string>("ProductName3");
                    if (string.IsNullOrEmpty(name1))
                        name1 = name;
                    if (string.IsNullOrEmpty(name2))
                        name2 = name;
                    if (string.IsNullOrEmpty(name3))
                        name3 = name;
                    row["ProductName"] = position == 1 ? prefixText + name : name + prefixText;
                    row["ProductName1"] = position == 1 ? prefixText + name1 : name1 + prefixText;
                    row["ProductName2"] = position == 1 ? prefixText + name2 : name2 + prefixText;
                    row["ProductName3"] = position == 1 ? prefixText + name3 : name3 + prefixText;
                }
            }
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
