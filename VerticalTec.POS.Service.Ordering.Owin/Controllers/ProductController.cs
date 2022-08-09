using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Service.Ordering.Owin;
using vtecPOS.GlobalFunctions;
using MySql.Data.MySqlClient;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    public class ProductController : ApiController
    {
        IDatabase _database;
        VtecPOSRepo _posRepo;
        POSModule _posModule;

        public ProductController(IDatabase database, POSModule posModule)
        {
            _database = database;
            _posRepo = new VtecPOSRepo(database);
            _posModule = posModule;
        }

        [HttpGet]
        [Route("v1/products/stock")]
        public async Task<IHttpActionResult> GetProductInfoStockAsync(int shopId, string keyword, int langId)
        {
            var response = new HttpActionResult<DataSet>(Request);
            var respText = "";
            var ds = new DataSet();
            using (var conn = await _database.ConnectAsync())
            {
                var saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, false, true);
                var success = _posModule.ProductInfo_Stock(ref respText, ref ds, shopId, saleDate, keyword, langId, conn as MySqlConnection);
                if (!success)
                    response.StatusCode = HttpStatusCode.BadRequest;
                response.Body = ds;
            }
            return response;
        }

        [HttpGet]
        [Route("v1/products/favorites")]
        public async Task<IHttpActionResult> GetFavoriteProductAsync(int shopId, int pageType = 2, SaleModes saleMode = SaleModes.DineIn)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var pageIndex = -1;

                var hqUrl = await _posRepo.GetBackofficeHQPathAsync(conn, shopId);

                DataTable dtPage = await _posRepo.GetFavoritePageIndexAsync(conn, shopId, pageType);
                DataTable dtFavorite = await _posRepo.GetFavoritProductsAsync(conn, shopId, pageType, pageIndex, saleMode);

                var favorite = new
                {
                    FavoritePages = (from index in dtPage.AsEnumerable()
                                     select new
                                     {
                                         ShopID = index.GetValue<int>("ShopID"),
                                         ComputerType = index.GetValue<int>("ComputerType"),
                                         PageType = index.GetValue<int>("PageType"),
                                         PageIndex = index.GetValue<int>("PageIndex"),
                                         PageName = index.GetValue<string>("PageName"),
                                         PageOrder = index.GetValue<int>("PageOrder"),
                                         ButtonColorCode = index.GetValue<string>("ButtonColorCode"),
                                         ButtonColorHexCode = index.GetValue<string>("ButtonColorHexCode")
                                     }),
                    FavoriteProducts = (from product in dtFavorite.AsEnumerable()
                                        select new
                                        {
                                            ShopID = product.GetValue<int>("ShopID"),
                                            ComputerType = product.GetValue<int>("ComputerType"),
                                            ProductID = product.GetValue<int>("ProductID"),
                                            ProductCode = product.GetValue<string>("ProductCode"),
                                            ProductTypeID = product.GetValue<int>("ProductTypeID"),
                                            ProductName = product.GetValue<string>("ProductName"),
                                            ProductName1 = product.GetValue<string>("ProductName1"),
                                            ProductName2 = product.GetValue<string>("ProductName2"),
                                            ProductName3 = product.GetValue<string>("ProductName3"),
                                            ProductImage = $"{hqUrl}{product.GetValue<string>("ProductPictureServer")}",
                                            ProductPrice = product["ProductPrice"] == DBNull.Value ? -1 : product.GetValue<decimal>("ProductPrice"),
                                            CurrentStock = product.GetValue<double>("CurrentStock"),
                                            EnableCountDownStock = product["CurrentStock"] != DBNull.Value,
                                            SaleMode = saleMode,
                                            PageType = product.GetValue<int>("PageType"),
                                            PageIndex = product.GetValue<int>("PageIndex"),
                                            ButtonOrder = product.GetValue<int>("ButtonOrder"),
                                            ButtonColorCode = product.GetValue<string>("ButtonColorCode"),
                                            ButtonColorHexCode = product.GetValue<string>("ButtonColorHexCode"),
                                            ImageFileName = product.GetValue<string>("ImageFileName"),
                                        }).ToList()
                };

                if (favorite.FavoritePages.Count() > 0)
                {
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = favorite;
                }
                else
                {
                    result.StatusCode = HttpStatusCode.NotFound;
                    result.Message = "No favorite data!";
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/products")]
        public async Task<IHttpActionResult> GetProductDataAsync(int shopId = 0, SaleModes saleMode = SaleModes.DineIn, int terminalId = 0)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var productIds = "";
                var productGroupIds = "";
                var productDeptIds = "";

                if (terminalId > 0)
                {
                    var dtComputerProduct = await _posRepo.GetComputerProductAsync(conn, terminalId);

                    var filterGroups = (from row in dtComputerProduct.AsEnumerable()
                                        group row by row.GetValue<int>("ProductGroupID") into g
                                        select g.Key.ToString()).ToArray();

                    foreach (var pgId in filterGroups)
                    {
                        productGroupIds += pgId + ",";
                    }
                    if (productGroupIds.EndsWith(","))
                        productGroupIds = productGroupIds.Substring(0, productGroupIds.Length - 1);

                    var filterDepts = (from row in dtComputerProduct.AsEnumerable()
                                       where filterGroups.Contains(row.GetValue<string>("ProductGroupID"))
                                       group row by row.GetValue<int>("ProductDeptID") into g
                                       select g.Key.ToString()).ToArray();
                    foreach (var filterDept in filterDepts)
                    {
                        productDeptIds += filterDept + ",";
                    }
                    if (productDeptIds.EndsWith(","))
                        productDeptIds = productDeptIds.Substring(0, productDeptIds.Length - 1);
                    if (productDeptIds == "0")
                        productDeptIds = "";

                    var filterProducts = (from row in dtComputerProduct.AsEnumerable()
                                          where filterDepts.Contains(row.GetValue<string>("ProductDeptID"))
                                          group row by row.GetValue<int>("ProductID") into g
                                          select g.Key.ToString()).ToArray();
                    foreach (var filterProduct in filterProducts)
                    {
                        productIds += filterProduct + ",";
                    }
                    if (productIds.EndsWith(","))
                        productIds = productIds.Substring(0, productIds.Length - 1);
                    if (productIds == "0")
                        productIds = "";
                }

                DataTable dtProductGroup = await _posRepo.GetProductGroupsAsync(conn, productGroupIds);
                DataTable dtProductDept = await _posRepo.GetProductDeptsAsync(conn, 0, productDeptIds);
                DataTable dtProducts = await _posRepo.GetProductsAsync(conn, shopId, 0, 0, productIds, saleMode);

                if (string.IsNullOrEmpty(productDeptIds))
                {
                    var groupIds = dtProductGroup.AsEnumerable().Select(g => g.GetValue<int>("ProductGroupID")).ToArray();
                    dtProductDept = dtProductDept.AsEnumerable().Where(d => groupIds.Contains(d.GetValue<int>("ProductGroupID"))).CopyToDataTable();
                }
                if (string.IsNullOrEmpty(productIds))
                {
                    var deptIds = dtProductDept.AsEnumerable().Select(d => d.GetValue<int>("ProductDeptID")).ToArray();
                    dtProducts = dtProducts.AsEnumerable().Where(p => deptIds.Contains(p.GetValue<int>("ProductDeptID"))).CopyToDataTable();
                }

                var hqUrl = await _posRepo.GetBackofficeHQPathAsync(conn, shopId);
                var products = new
                {
                    ProductGroups = (from groupRow in dtProductGroup.AsEnumerable()
                                     select new
                                     {
                                         ProductGroupID = groupRow.GetValue<int>("ProductGroupID"),
                                         ProductGroupCode = groupRow.GetValue<string>("ProductGroupCode"),
                                         ProductGroupName = groupRow.GetValue<string>("ProductGroupName"),
                                     }).ToList(),
                    ProductDepts = (from dept in dtProductDept.AsEnumerable()
                                    select new
                                    {
                                        ProductGroupID = dept.GetValue<int>("ProductGroupID"),
                                        ProductDeptID = dept.GetValue<int>("ProductDeptID"),
                                        ProductDeptName = dept.GetValue<string>("ProductDeptName")
                                    }).ToList(),
                    Products = (from item in dtProducts.AsEnumerable()
                                select new
                                {
                                    ProductGroupID = item.GetValue<int>("ProductGroupID"),
                                    ProductDeptID = item.GetValue<int>("ProductDeptID"),
                                    ProductID = item.GetValue<int>("ProductID"),
                                    ProductTypeID = item.GetValue<int>("ProductTypeID"),
                                    ProductCode = item.GetValue<string>("ProductCode"),
                                    ProductName = item.GetValue<string>("ProductName"),
                                    ProductName1 = item.GetValue<string>("ProductName1"),
                                    ProductName2 = item.GetValue<string>("ProductName2"),
                                    ProductName3 = item.GetValue<string>("ProductName3"),
                                    ProductImage = $"{hqUrl}{item.GetValue<string>("ProductPictureServer")}",
                                    ProductPrice = item["ProductPrice"] == DBNull.Value ? -1 : item.GetValue<decimal>("ProductPrice"),
                                    CurrentStock = item.GetValue<double>("CurrentStock"),
                                    EnableCountDownStock = item["CurrentStock"] != DBNull.Value,
                                    SaleMode = saleMode,
                                    RequireAddAmount = item.GetValue<int>("RequireAddAmount")
                                }).ToList()
                };
                result.StatusCode = HttpStatusCode.OK;
                result.Body = products;
            }
            return result;
        }

        [HttpGet]
        [Route("v1/products/combosets")]
        public async Task<IHttpActionResult> GetProductComboSetAsync(int shopId, int parentProductId, SaleModes saleMode = SaleModes.DineIn)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var dtComponentGroup = await _posRepo.GetProductComponentGroupAsync(conn, parentProductId);
                var dtComponent = await _posRepo.GetProductComponentAsync(conn, shopId, 0, parentProductId, saleMode);

                var list = (from pgroup in dtComponentGroup.AsEnumerable()
                            select new
                            {
                                PGroupID = pgroup.GetValue<int>("PGroupID"),
                                PGroupTypeID = pgroup.GetValue<int>("PGroupTypeID"),
                                ProductID = pgroup.GetValue<int>("ProductID"),
                                SaleMode = pgroup.GetValue<int>("SaleMode"),
                                SetGroupNo = pgroup.GetValue<string>("SetGroupNo"),
                                SetGroupName = pgroup.GetValue<string>("SetGroupName"),
                                RequireAmount = pgroup.GetValue<int>("RequireAddAmountForProduct"),
                                MinQty = pgroup.GetValue<int>("MinQty"),
                                MaxQty = pgroup.GetValue<int>("MaxQty"),
                                IsDefault = pgroup.GetValue<int>("IsDefault"),
                                ProductComponents = (from component in dtComponent.AsEnumerable()
                                                     where component.GetValue<int>("PGroupID") == pgroup.GetValue<int>("PGroupID")
                                                     select new
                                                     {
                                                         PGroupID = pgroup.GetValue<int>("PGroupID"),
                                                         SetGroupNo = pgroup.GetValue<string>("SetGroupNo"),
                                                         ShopID = shopId,
                                                         ProductID = component.GetValue<int>("ProductID"),
                                                         ProductTypeID = component.GetValue<int>("ProductTypeID"),
                                                         MaterialAmount = component.GetValue<decimal>("MaterialAmount"),
                                                         SaleMode = component.GetValue<int>("SaleMode"),
                                                         QtyRatio = component.GetValue<decimal>("QtyRatio"),
                                                         ProductPrice = component.GetValue<decimal>("ProductPrice"),
                                                         ProductCode = component.GetValue<string>("ProductCode"),
                                                         ProductName = component.GetValue<string>("ProductName"),
                                                         ProductName1 = component.GetValue<string>("ProductName1"),
                                                         ProductName2 = component.GetValue<string>("ProductName2"),
                                                         ProductName3 = component.GetValue<string>("ProductName3"),
                                                         ProductImage = component.GetValue<string>("ProductImage")
                                                     }).ToList()
                            }).ToList();
                if (list.Count > 0)
                {
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = list;
                }
                else
                {
                    result.StatusCode = HttpStatusCode.NotFound;
                    result.Message = "No comboset data!";
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/products/components")]
        public async Task<IHttpActionResult> GetProductComponentAsync(int shopId, int parentProductId, SaleModes saleMode = SaleModes.DineIn)
        {
            var result = new HttpActionResult<DataTable>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var dtComponent = await _posRepo.GetProductComponentAsync(conn, shopId, 0, parentProductId, saleMode);
                result.StatusCode = HttpStatusCode.OK;
                result.Body = dtComponent;
            }
            return result;
        }

        [HttpGet]
        [Route("v1/products/kiosk/menu")]
        public async Task<IHttpActionResult> GetMenuTemplateAsync(int shopId, int saleMode = 1, int terminalId = 0)
        {
            var result = new HttpActionResult<List<object>>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var cmd = _database.CreateCommand("select a.Kiosk_TemplateID" +
                            " from kiosk_template a" +
                            " inner join kiosk_template_shoplink b" +
                            " on a.Kiosk_TemplateID=b.Kiosk_TemplateID" +
                            " where a.Deleted=0" +
                            " and a.Kiosk_StartDate <= @date and (CASE WHEN a.Kiosk_EndDate IS NULL THEN DATE_FORMAT(NOW(),'%Y-%m-%d') END) >= @date" +
                            " and b.ShopID=@shopId", conn);
                    cmd.Parameters.Add(_database.CreateParameter("@date", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                    cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                    DataTable dtTemplate = new DataTable();
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        dtTemplate.Load(reader);
                    }
                    if (dtTemplate.Rows.Count == 0)
                        throw new Exception("Not found kiosk template configuration!");

                    int templateId = dtTemplate.Rows[0].GetValue<int>("Kiosk_TemplateID");
                    string imageBaseUrl = await _posRepo.GetResourceUrl(conn, shopId);
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

                    var saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, false, true);
                    cmd.Parameters.Add(_database.CreateParameter("@templateId", templateId));
                    cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
                    cmd.Parameters.Add(_database.CreateParameter("@saleMode", saleMode));
                    cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));

                    IDataAdapter adapter = _database.CreateDataAdapter(cmd);
                    adapter.TableMappings.Add("Table", "MenuPages");
                    adapter.TableMappings.Add("Table1", "MenuPageDetails");
                    DataSet dsMenuTemplate = new DataSet("MenuTemplate");
                    adapter.Fill(dsMenuTemplate);

                    var dtUpsales = await GetUpsalesMenuAsync(conn, shopId, saleDate, saleMode);
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
                            IsSuggestion = pageRow.GetValue<int>("IsSuggestion"),
                            NoRows = pageRow.GetValue<int>("NoRow"),
                            NoColumns = pageRow.GetValue<int>("NoColumns"),
                            MenuPageDetails = new List<object>()
                        };
                        var pageDetails = dsMenuTemplate.Tables["MenuPageDetails"]
                                                    .Select($"Kiosk_TemplateID={pageRow.GetValue<int>("Kiosk_TemplateID")} and PageID={pageRow.GetValue<int>("PageID")}");
                        foreach (DataRow detailRow in pageDetails)
                        {
                            var pageId = detailRow.GetValue<int>("PageID");
                            var detailId = detailRow.GetValue<int>("DetailID");
                            var upsales = new List<object>();
                            foreach (var sugest in dtUpsales.Select($"PageID={pageId} and PageDetailID={detailId}"))
                            {
                                upsales.Add(new
                                {
                                    ProductID = sugest.GetValue<int>("ProductID"),
                                    ProductCode = sugest.GetValue<string>("ProductCode"),
                                    ProductName = sugest.GetValue<string>("ProductName"),
                                    ProductPrice = sugest.GetValue<decimal>("ProductPrice"),
                                    ProductTypeID = sugest.GetValue<int>("ProductTypeID"),
                                    MenuImageUrl = sugest.GetValue<string>("MenuImageUrl")
                                });
                            }

                            var pageDetail = new
                            {
                                DetailImage = detailRow.GetValue<string>("DetailImage"),
                                PageID = detailRow.GetValue<int>("PageID"),
                                DetailID = detailId,
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
                                CurrentStock = detailRow["CurrentStock"] as int?,
                                UpsaleMenus = upsales
                            };
                            page.MenuPageDetails.Add(pageDetail);
                        }
                        pages.Add(page);
                    }
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = pages;
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

        [HttpGet]
        [Route("v1/products/kiosk/combosets")]
        public async Task<IHttpActionResult> GetKioskProductComboSetAsync(int shopId, int parentProductId = 0)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var imageUrlBase = await _posRepo.GetResourceUrl(conn, shopId);
                    var cmd = _database.CreateCommand(
                        "select *" +
                        " from productcomponentgroup " + (parentProductId > 0 ? " where ProductID = @parentProductId;" : ";") +
                        " select a.PGroupID, a.MaterialAmount," +
                        " case when a.FlexibleProductPrice = 0 then -1 else a.FlexibleProductPrice end as ProductPrice," +
                        " a.QtyRatio, b.ProductID, b.ProductCode, " +
                        " b.ProductName, b.ProductName as ProductName1, b.ProductName2, b.ProductName3, b.ProductTypeID," +
                        " concat(@imageUrlBase, b.ProductPictureServer) as ProductImage," +
                        " d.CurrentStock" +
                        " from productcomponent a" +
                        " inner join products b" +
                        " on a.MaterialID=b.ProductID" +
                        " left join (select ProductID, DisplayName, DisplayName1, DisplayName2, DisplayName3, DetailImage from kiosk_pagedetail where GoPageID=0 and Deleted=0) c" +
                        " on b.ProductID=c.ProductID" +
                        " left join productcountdownstock d " +
                        " on b.ProductID=d.ProductID" +
                        " and d.ShopID=@shopId " +
                        " where b.Deleted=0 " + (parentProductId > 0 ? " and a.ProductID=@parentProductId" : "") + " order by a.Ordering; ", conn);
                    string saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, false);
                    if (parentProductId > 0)
                        cmd.Parameters.Add(_database.CreateParameter("@parentProductId", parentProductId));
                    cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                    cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));
                    cmd.Parameters.Add(_database.CreateParameter("@imageUrlBase", imageUrlBase));

                    var adapter = _database.CreateDataAdapter(cmd);
                    adapter.TableMappings.Add("Table", "ProductComponentGroup");
                    adapter.TableMappings.Add("Table1", "ProductComponent");
                    var dataSet = new DataSet();
                    adapter.Fill(dataSet);

                    var list = (from pgroup in dataSet.Tables["ProductComponentGroup"].AsEnumerable()
                                select new
                                {
                                    PGroupID = pgroup.GetValue<int>("PGroupID"),
                                    PGroupTypeID = pgroup.GetValue<int>("PGroupTypeID"),
                                    ProductID = pgroup.GetValue<int>("ProductID"),
                                    SaleMode = pgroup.GetValue<int>("SaleMode"),
                                    SetGroupNo = pgroup.GetValue<string>("SetGroupNo"),
                                    SetGroupName = pgroup.GetValue<string>("SetGroupName"),
                                    RequireAmount = pgroup.GetValue<int>("RequireAddAmountForProduct"),
                                    MinQty = pgroup.GetValue<int>("MinQty"),
                                    MaxQty = pgroup.GetValue<int>("MaxQty"),
                                    IsDefault = pgroup.GetValue<int>("IsDefault"),
                                    ProductComponents = (from component in dataSet.Tables["ProductComponent"].AsEnumerable()
                                                         where component.GetValue<int>("PGroupID") == pgroup.GetValue<int>("PGroupID")
                                                         select new
                                                         {
                                                             PGroupID = pgroup.GetValue<int>("PGroupID"),
                                                             SetGroupNo = pgroup.GetValue<string>("SetGroupNo"),
                                                             ShopID = shopId,
                                                             ProductID = component.GetValue<int>("ProductID"),
                                                             ProductTypeID = component.GetValue<int>("ProductTypeID"),
                                                             MaterialAmount = component.GetValue<decimal>("MaterialAmount"),
                                                             SaleMode = component.GetValue<int>("SaleMode"),
                                                             QtyRatio = component.GetValue<decimal>("QtyRatio"),
                                                             ProductPrice = component.GetValue<decimal>("ProductPrice"),
                                                             ProductCode = component.GetValue<string>("ProductCode"),
                                                             ProductName = component.GetValue<string>("ProductName"),
                                                             ProductName1 = component.GetValue<string>("ProductName1"),
                                                             ProductName2 = component.GetValue<string>("ProductName2"),
                                                             ProductName3 = component.GetValue<string>("ProductName3"),
                                                             ProductImage = component.GetValue<string>("ProductImage"),
                                                             CurrentStock = component["CurrentStock"]
                                                         }).ToList()
                                }).ToList();

                    if (list.Count > 0)
                    {
                        result.StatusCode = HttpStatusCode.OK;
                        result.Body = list;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.NotFound;
                        result.Message = "No comboset data!";
                    }
                }
                catch (Exception ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
        }

        async Task<DataTable> GetUpsalesMenuAsync(IDbConnection conn, int shopId, string saleDate, int saleMode)
        {
            string dbName = AppConfig.Instance.DbName;
            var tableName = "kiosk_suggestion_menu_setting";
            var cmd = _database.CreateCommand(conn);
            cmd.CommandText = "SELECT * FROM information_schema.tables WHERE table_schema = @dbName " +
                "AND TABLE_NAME = @tableName LIMIT 1; ";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(_database.CreateParameter("@dbName", dbName));
            cmd.Parameters.Add(_database.CreateParameter("@tableName", tableName));

            bool alreadyHaveTable = false;
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    alreadyHaveTable = true;
                }
            }

            var createTableSql = "create table " + tableName + "(" +
                    "PageDetailID int(11), " +
                    "ProductID int(11)," +
                    "PageID int(11), " +
                    "primary key (PageDetailID, ProductID, PageID)" +
                    ")";
            if (!alreadyHaveTable)
            {
                cmd.CommandText = createTableSql;
                cmd.ExecuteNonQuery();
            }
            else
            {
                cmd.CommandText = $"select * from {tableName} limit 1";
                var pageIdCol = "";
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        try
                        {
                            pageIdCol = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).Where(c => c.Equals("PageID")).SingleOrDefault();
                        }
                        catch { }
                    }
                }

                if (string.IsNullOrEmpty(pageIdCol))
                {
                    cmd.CommandText = $"drop table {tableName}";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = createTableSql;
                    cmd.ExecuteNonQuery();
                }
            }

            string imageBaseUrl = await _posRepo.GetResourceUrl(conn, shopId);
            cmd = _database.CreateCommand(
                               " SELECT a.PageID, a.PageDetailID, b.ProductID, b.DisplayName AS ProductName, " +
                               " CONCAT('" + imageBaseUrl + "', b.DetailImage) AS MenuImageUrl, c.ProductTypeID, " +
                               " CASE WHEN d.ProductPrice IS NOT NULL THEN d.ProductPrice ELSE 0 END AS ProductPrice " +
                               " FROM kiosk_suggestion_menu_setting a " +
                               " LEFT JOIN kiosk_pagedetail b " +
                               " ON a.ProductID = b.ProductID " +
                               " INNER JOIN products c " +
                               " ON b.ProductID = c.ProductID " +
                               " LEFT JOIN (SELECT ProductID, ProductPrice FROM productprice WHERE FromDate <= @saleDate AND ToDate >= @saleDate AND SaleMode = @dfSaleMode) d " +
                               " ON c.ProductID = d.ProductID WHERE b.Activated = 1 AND b.Deleted = 0 " +
                               " ORDER BY b.RowPosition, b.DisplayName; ", conn);

            cmd.Parameters.Clear();
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_database.CreateParameter("@dfSaleMode", saleMode));
            cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));

            var dt = new DataTable();
            using (var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dt.Load(reader);
            }
            return dt;
        }
    }
}
