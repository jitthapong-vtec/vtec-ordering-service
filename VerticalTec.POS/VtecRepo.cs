using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS
{
    public class VtecRepo
    {
        IDatabase _database;

        public VtecRepo(IDatabase database)
        {
            _database = database;
        }

        public async Task<IEnumerable<object>> GetKioskPageAsync(IDbConnection conn, int shopId, SaleModes saleMode = SaleModes.DineIn)
        {
            var cmd = _database.CreateCommand("select a.Kiosk_TemplateID" +
                                " from kiosk_template a" +
                                " inner join kiosk_template_shoplink b" +
                                " on a.Kiosk_TemplateID=b.Kiosk_TemplateID" +
                                " where a.Deleted=0" +
                                " and a.Kiosk_StartDate <= @date and a.Kiosk_EndDate >= @date" +
                                " and b.ShopID=@shopId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            DataTable dtTemplate = new DataTable();
            using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtTemplate.Load(reader);
            }
            if (dtTemplate.Rows.Count == 0)
                throw new VtecPOSException("Not found kiosk template configuration!");

            int templateId = dtTemplate.Rows[0].GetValue<int>("Kiosk_TemplateID");
            var rootDir = await GetPropertyValueAsync(conn, 1012, "RootWebDir", shopId);
            var backoffice = await GetPropertyValueAsync(conn, 1012, "BackOfficePath", shopId);
            var saleDate = await GetSaleDateAsync(conn, shopId, chkCurrDate: false);

            string imageBaseUrl = $"{rootDir}/{backoffice}/UploadImage/Kiosk/Products/";
            cmd = _database.CreateCommand(
                    " select concat('" + imageBaseUrl + "', a.PageImage) as PageImage, a.*, b.LayoutTypeName, " +
                    " case when b.NoRows is null then 4 else b.NoRows end as NoRows, " +
                    " case when b.NoColumns is null then 3 else b.NoColumns end as NoColumns" +
                    " from kiosk_page a left join kiosk_layout b" +
                    " on a.LayoutType=b.LayoutTypeID where a.Activated=1 and a.Deleted=0 and a.Kiosk_TemplateID=@templateId " +
                    " order by a.PageOrdering;" +
                    " select concat('" + imageBaseUrl + "', a.DetailImage) as DetailImage, a.*, " +
                    " b.ProductGroupID, b.ProductDeptID, b.ProductTypeID, b.ProductCode, b.AutoComment, " +
                    " case when c.ProductPrice is not null then c.ProductPrice else" +
                    " case when d.ProductPrice is not null then d.ProductPrice else -1 end end as ProductPrice, " +
                    " case when b.Deleted = 0 and b.ProductActivate = 1 then 1 else case when a.GoPageID = 0 then 0 else 1 end end as Available," +
                    " b.ProductActivate, b.SaleMode1, b.SaleMode2, e.CurrentStock " +
                    " from kiosk_pagedetail a" +
                    " left join products b" +
                    " on a.ProductID=b.ProductID" +
                    " left join " +
                    " (select ProductID, ProductPrice from productprice where FromDate <= @saleDate and ToDate >= @saleDate and SaleMode=@saleMode) c on b.ProductID = c.ProductID " +
                    " left join " +
                    " (select ProductID, ProductPrice from productprice where FromDate <= @saleDate and ToDate >= @saleDate and SaleMode=1) d on b.ProductID = d.ProductID " +
                    " left join productcountdownstock e" +
                    " on b.ProductID=e.ProductID" +
                    " and e.ShopID=@shopId " +
                    " where a.Activated=1 and a.Deleted=0 and a.Kiosk_TemplateID=@templateId " +
                    " order by a.RowPosition", conn);

            cmd.Parameters.Add(_database.CreateParameter("@templateId", templateId));
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
            cmd.Parameters.Add(_database.CreateParameter("@saleMode", saleMode));
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));

            IDataAdapter adapter = _database.CreateDataAdapter(cmd);
            adapter.TableMappings.Add("Table", "MenuPages");
            adapter.TableMappings.Add("Table1", "MenuPageDetails");
            DataSet dsMenuTemplate = new DataSet("MenuTemplate");
            adapter.Fill(dsMenuTemplate);

            var pages = new List<object>();
            foreach (DataRow pageRow in dsMenuTemplate.Tables["MenuPages"].Rows)
            {
                var page = new
                {
                    PageImage = pageRow.GetValue<string>("PageImage"),
                    PageID = pageRow.GetValue<int>("PageID"),
                    LayoutType = pageRow.GetValue<int>("LayoutType"),
                    PageName = pageRow.GetValue<string>("PageName"),
                    PageDesp = pageRow.GetValue<string>("PageDesp"),
                    PageName1 = pageRow.GetValue<string>("PageName1"),
                    PageDesp1 = pageRow.GetValue<string>("PageDesp1"),
                    PageName2 = pageRow.GetValue<string>("PageName2"),
                    PageDesp2 = pageRow.GetValue<string>("PageDesp2"),
                    PageName3 = pageRow.GetValue<string>("PageName3"),
                    PageDesp3 = pageRow.GetValue<string>("PageDesp3"),
                    PageLevelID = pageRow.GetValue<int>("PageLevelID"),
                    NoRows = pageRow.GetValue<int>("NoRow"),
                    NoColumns = pageRow.GetValue<int>("NoColumns"),
                    MenuPageDetails = new List<object>()
                };
                var pageDetails = dsMenuTemplate.Tables["MenuPageDetails"]
                                            .Select($"Kiosk_TemplateID={pageRow.GetValue<int>("Kiosk_TemplateID")} and PageID={pageRow.GetValue<int>("PageID")}");
                foreach (DataRow detailRow in pageDetails)
                {
                    var pageDetail = new
                    {
                        DetailImage = detailRow.GetValue<string>("DetailImage"),
                        PageID = detailRow.GetValue<int>("PageID"),
                        DetailID = detailRow.GetValue<int>("DetailID"),
                        ProductID = detailRow.GetValue<int>("ProductID"),
                        GoPageID = detailRow.GetValue<int>("GoPageID"),
                        DisplayName = detailRow.GetValue<string>("DisplayName"),
                        DisplayDesp = detailRow.GetValue<string>("DisplayDesp"),
                        DisplayName1 = detailRow.GetValue<string>("DisplayName1"),
                        DisplayDesp1 = detailRow.GetValue<string>("DisplayDesp1"),
                        DisplayName2 = detailRow.GetValue<string>("DisplayName2"),
                        DisplayDesp2 = detailRow.GetValue<string>("DisplayDesp2"),
                        DisplayName3 = detailRow.GetValue<string>("DisplayName3"),
                        DisplayDesp3 = detailRow.GetValue<string>("DisplayDesp3"),
                        ColPosition = detailRow.GetValue<int>("ColPosition"),
                        RowPosition = detailRow.GetValue<int>("RowPosition"),
                        ProductGroupID = detailRow.GetValue<int>("ProductGroupID"),
                        ProductDeptID = detailRow.GetValue<int>("ProductDeptID"),
                        ProductTypeID = detailRow.GetValue<int>("ProductTypeID"),
                        ProductCode = detailRow.GetValue<string>("ProductCode"),
                        AutoComment = detailRow.GetValue<int>("AutoComment"),
                        ProductPrice = detailRow.GetValue<decimal>("ProductPrice"),
                        Available = detailRow.GetValue<int>("Available"),
                        SaleMode1 = detailRow.GetValue<int>("SaleMode1"),
                        SaleMode2 = detailRow.GetValue<int>("SaleMode2"),
                        CurrentStock = detailRow["CurrentStock"] as int?
                    };
                    page.MenuPageDetails.Add(pageDetail);
                }
                pages.Add(page);
            }
            return pages;
        }

        public async Task<string> GetSaleDateAsync(IDbConnection conn, int shopId, bool chkCurrDate = false, bool withBracket = false)
        {
            string saleDate = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var cmd = new MySqlCommand("select SessionDate from sessionenddaydetail " +
                "where ShopID=@shopId and IsEndDay=@isEndDay order by sessiondate desc limit 1", conn as MySqlConnection);
            cmd.Parameters.AddWithValue("@shopId", shopId);
            cmd.Parameters.AddWithValue("@isEndDay", 0);
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    DateTime currentDate = DateTime.Now;
                    DateTime lastSaleDate = reader.GetDateTime(0);
                    var lastSaleDateEarlyNow = DateTime.Compare(lastSaleDate.Date, currentDate.Date) < 0;
                    if (chkCurrDate == true && lastSaleDateEarlyNow)
                        throw new VtecPOSException("The front program did not open sale day!");

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
