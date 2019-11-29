using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vtecdbhelper;
using VerticalTec.POS.Utils;
using System.Globalization;
using VerticalTec.POS.Report.Dashboard.Models;
using VerticalTec.POS.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace VerticalTec.POS.Report.Dashboard.Controllers
{
    public class ReportController : ApiControllerBase
    {
        IDbHelper _db;
        IDatabase _db2;
        VtecPOSRepo _posRepo;

        public ReportController(IDbHelper db, IDatabase db2)
        {
            _db = db;
            _db2 = db2;
            _posRepo = new VtecPOSRepo(db2);
        }

        [HttpGet()]
        [ActionName("shopdata")]
        public async Task<IActionResult> GetShopAsync(int staffId)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var report = new VTECReports.Reports(_db);
                    var dataSet = report.Shop_Info(staffId, conn);
                    var shopList = new List<object>();
                    foreach (DataRow row in dataSet.Tables["ShopData"].Rows)
                    {
                        shopList.Add(new
                        {
                            shopId = row.GetValue<int>("ShopID"),
                            shopName = row.GetValue<string>("ShopName")
                        });
                    }
                    return Ok(shopList);
                }
            }
            catch (Exception ex)
            {
                return NoContent();
            }
        }

        [HttpGet]
        [ActionName("bills")]
        public async Task<IActionResult> GetBillReportAsync(int staffId, string shopIds, DateTime startDate, DateTime endDate, int reportType = 0, int langId = 1, string parentId = "")
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var report = new VTECReports.Reports(_db);

                    var cate = new Dictionary<int, string>();
                    shopIds = await ValidateShopIds(staffId, shopIds);
                    var fromDateStr = ToISODate(startDate);
                    var toDateStr = ToISODate(endDate);
                    var prop = await _posRepo.GetProgramPropertyAsync(conn);
                    var dateFormat = prop.Select("PropertyID = 13").FirstOrDefault()?.GetValue<string>("PropertyTextValue");
                    var qtyFormat = prop.Select("PropertyID = 15").FirstOrDefault()?.GetValue<string>("PropertyTextValue");
                    var currencyFormat = prop.Select("PropertyID = 12").FirstOrDefault()?.GetValue<string>("PropertyTextValue");

                    var dsDetail = report.Report_BillData(shopIds, fromDateStr, toDateStr, 0, langId, cate, conn);
                    var receiptGrouping = (from receipt in dsDetail.Tables["BillData"].AsEnumerable()
                                           group receipt by new { ShopId = receipt.GetValue<string>("ShopID"), SaleDate = receipt.GetValue<DateTime>("SaleDate") } into g
                                           select new { ShopId = g.Key.ShopId, ShopName = g.FirstOrDefault().GetValue<string>("ShopName"), SaleDate = g.Key.SaleDate, Receipts = g });

                    var ds = report.Report_BillData(shopIds, fromDateStr, toDateStr, reportType, langId, cate, conn);

                    var reports = new List<BillReportTree>();
                    if (string.IsNullOrEmpty(parentId))
                    {
                        foreach (DataRow row in ds.Tables["BillData"].Rows)
                        {
                            var date = DateTime.MinValue;
                            if (reportType == 2)
                            {
                                try
                                {
                                    date = DateTime.ParseExact(row.GetValue<string>("Description"), "dd-MM-yyyy", null);
                                }
                                catch (Exception) { }
                            }

                            reports.Add(new BillReportTree
                            {
                                Id = reportType == 1 ? row["ShopID"] : date.ToString("yyyy-MM-dd"),
                                Description = reportType == 1 ? row.GetValue<string>("Description") : date.ToString(dateFormat),
                                TotalQty = row.GetValue<decimal>("TotalQty").ToString(qtyFormat),
                                TotalAmount = row.GetValue<decimal>("TotalAmount").ToString(currencyFormat),
                                HasItem = true
                            });
                        }
                    }
                    else
                    {
                        IGrouping<object, DataRow> reportDetail = null;
                        if (reportType == 1)
                        {
                            if (!parentId.StartsWith("child"))
                            {
                                var dateGrouping = (from receipt in receiptGrouping
                                                    where receipt.ShopId == parentId
                                                    select receipt);
                                foreach (var receipt in dateGrouping)
                                {
                                    reports.Add(new BillReportTree
                                    {
                                        Id = $"child_{parentId}_{receipt.SaleDate.ToString("yyyy-MM-dd")}",
                                        ParentId = parentId,
                                        Description = receipt.SaleDate.ToString(dateFormat),
                                        TotalQty = receipt.Receipts.Where(s => s.GetValue<int>("TransactionStatusID") == 2).Sum(s => s.GetValue<decimal>("ReceiptTotalQty")).ToString(qtyFormat),
                                        TotalAmount = receipt.Receipts.Where(s => s.GetValue<int>("TransactionStatusID") == 2).Sum(s => s.GetValue<decimal>("ReceiptPayPrice")).ToString(currencyFormat),
                                        HasItem = true
                                    });
                                }
                            }
                            else
                            {
                                var searchParams = parentId.Split('_');
                                reportDetail = (from receipt in receiptGrouping
                                                where receipt.ShopId == searchParams[1] && receipt.SaleDate.ToString("yyyy-MM-dd") == searchParams[2]
                                                select receipt.Receipts).FirstOrDefault();
                            }
                        }
                        else if (reportType == 2)
                        {
                            if (!parentId.StartsWith("child"))
                            {
                                var shopGrouping = (from receipt in receiptGrouping
                                                    where receipt.SaleDate.ToString("yyyy-MM-dd") == parentId
                                                    select receipt);
                                foreach (var receipt in shopGrouping)
                                {
                                    reports.Add(new BillReportTree
                                    {
                                        Id = $"child_{parentId}_{receipt.ShopId}",
                                        ParentId = parentId,
                                        Description = receipt.ShopName,
                                        TotalQty = receipt.Receipts.Where(s => s.GetValue<int>("TransactionStatusID") == 2).Sum(s => s.GetValue<decimal>("ReceiptTotalQty")).ToString(qtyFormat),
                                        TotalAmount = receipt.Receipts.Where(s => s.GetValue<int>("TransactionStatusID") == 2).Sum(s => s.GetValue<decimal>("ReceiptPayPrice")).ToString(currencyFormat),
                                        HasItem = true
                                    });
                                }
                            }
                            else
                            {
                                var searchParams = parentId.Split('_');
                                reportDetail = (from receipt in receiptGrouping
                                                where receipt.SaleDate.ToString("yyyy-MM-dd") == searchParams[1] && receipt.ShopId == searchParams[2]
                                                select receipt.Receipts).FirstOrDefault();
                            }
                        }

                        if (reportDetail != null)
                        {
                            foreach (var detail in reportDetail)
                            {
                                reports.Add(new BillReportTree
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ParentId = parentId,
                                    Description = detail.GetValue<string>("ReceiptNumber"),
                                    TotalQty = detail.GetValue<decimal>("ReceiptTotalQty").ToString(qtyFormat),
                                    TotalAmount = detail.GetValue<decimal>("ReceiptPayPrice").ToString(currencyFormat)
                                });
                            }
                        }
                    }
                    return Ok(reports);
                }
            }
            catch (Exception ex)
            {
                return NoContent();
            }
        }

        [HttpGet]
        [ActionName("hourly")]
        public async Task<IActionResult> GetHourlyReport(int staffId, string shopIds, DateTime startDate, DateTime endDate, int reportType = 0, int langId = 1)
        {
            var result = new ReportActionResult<object>();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var report = new VTECReports.Reports(_db);

                    var cate = new Dictionary<int, string>();

                    shopIds = await ValidateShopIds(staffId, shopIds);
                    var fromDateStr = ToISODate(startDate);
                    var toDateStr = ToISODate(endDate);
                    var prop = await _posRepo.GetProgramPropertyAsync(conn, 12);
                    var currencyFormat = prop.Rows[0].GetValue<string>("PropertyTextValue");

                    var ds = report.Report_HourlyData(shopIds, fromDateStr, toDateStr, reportType, langId, cate, conn);
                    result.Data = new
                    {
                        raw = (from row in ds.Tables["HourlyData"].AsEnumerable()
                               select new {Hourly=row.GetValue<int>("Hourly"), TotalBill = row.GetValue<int>("TotalBill"), TotalSale=row.GetValue<decimal>("TotalSale") }),
                        stat = new
                        {
                            Summary = ds.Tables["StatData"].Rows[0].GetValue<decimal>("DataValue").ToString(currencyFormat),
                            AvgPerHour = ds.Tables["StatData"].Rows[1].GetValue<decimal>("DataValue").ToString(currencyFormat)
                        },
                        chartData = (from row in ds.Tables["GraphData"].AsEnumerable()
                                     select new { Description = row.GetValue<string>("Description"), Percentage = row.GetValue<decimal>("Percentage") }),
                        html = ds.Tables["htmlData"].Select("ReportType='HourlyData'").FirstOrDefault()?.GetValue<string>("HtmlData")
                    };
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = StatusCodes.Status500InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

        [HttpGet]
        [ActionName("summary")]
        public async Task<IActionResult> GetSummaryReportAsync(int staffId, string shopIds, DateTime startDate, DateTime endDate, int langId = 1)
        {
            var result = new ReportActionResult<object>();
            var saleByGroupHtml = "";
            var promoDataHtml = "";
            var statDataHtml = "";
            var saleModeDataHtml = "";
            try
            {
                var ds = new DataSet();
                using (var conn = await _db.ConnectAsync())
                {
                    var cate = new Dictionary<int, string>();
                    var report = new VTECReports.Reports(_db);

                    shopIds = await ValidateShopIds(staffId, shopIds);
                    var fromDateStr = ToISODate(startDate);
                    var toDateStr = ToISODate(endDate);
                    ds = report.Report_SummarySales(shopIds, fromDateStr, toDateStr, langId, cate, conn);
                    var dtHtml = ds.Tables["htmlData"];
                    saleByGroupHtml = dtHtml.Select("ReportType='SaleByGroup'").FirstOrDefault()?.GetValue<string>("HtmlData");
                    promoDataHtml = dtHtml.Select("ReportType='PromoData'").FirstOrDefault()?.GetValue<string>("HtmlData");
                    statDataHtml = dtHtml.Select("ReportType='StatData'").FirstOrDefault()?.GetValue<string>("HtmlData");
                    saleModeDataHtml = dtHtml.Select("ReportType='SaleModeData'").FirstOrDefault()?.GetValue<string>("HtmlData");
                }

                result.Data = new
                {
                    productSaleChartData = (from row in ds.Tables["ProductCatGraphData"].AsEnumerable()
                                            select new
                                            {
                                                ProductCatName = row.GetValue<string>("ProductCatName"),
                                                Percentage = row.GetValue<decimal>("Percentage")
                                            }),
                    saleByGroupHtml = saleByGroupHtml,
                    promoDataHtml = promoDataHtml,
                    statDataHtml = statDataHtml,
                    saleModeDataHtml = saleModeDataHtml
                };
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.StatusCode = StatusCodes.Status500InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

        [HttpGet]
        [ActionName("tender")]
        public async Task<IActionResult> GetTenderReportAsync(int staffId, string shopIds, DateTime startDate, DateTime endDate, int langId = 1)
        {
            var result = new ReportActionResult<string>();
            var reportHtml = new StringBuilder();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var cate = new Dictionary<int, string>();
                    var report = new VTECReports.Reports(_db);

                    shopIds = await ValidateShopIds(staffId, shopIds);
                    var fromDateStr = ToISODate(startDate);
                    var toDateStr = ToISODate(endDate);
                    var ds = report.Report_TenderData(shopIds, fromDateStr, toDateStr, langId, cate, conn);
                    foreach (DataRow html in ds.Tables["htmlData"].Rows)
                    {
                        reportHtml.Append(html.GetValue<string>("HtmlData") + "</br>");
                    }
                }
                result.Data = reportHtml.ToString();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.StatusCode = StatusCodes.Status500InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

        [HttpGet]
        [ActionName("audit")]
        public async Task<IActionResult> GetAuditReportAsync(int staffId, string shopIds, DateTime startDate, DateTime endDate, int langId = 1)
        {
            var result = new ReportActionResult<string>();
            var reportHtml = new StringBuilder();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var cate = new Dictionary<int, string>();
                    var report = new VTECReports.Reports(_db);

                    shopIds = await ValidateShopIds(staffId, shopIds);
                    var fromDateStr = ToISODate(startDate);
                    var toDateStr = ToISODate(endDate);

                    var ds = report.Report_AuditData(shopIds, fromDateStr, toDateStr, langId, cate, conn);
                    reportHtml.Append(ds.Tables["htmlData"].Select("ReportType='voidData'").FirstOrDefault().GetValue<string>("HtmlData"));
                }
                result.Data = reportHtml.ToString();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.StatusCode = StatusCodes.Status500InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

        async Task<string> ValidateShopIds(int staffId, string shopIds)
        {
            if (string.IsNullOrEmpty(shopIds) || shopIds == "0" || shopIds == "null")
            {
                shopIds = "";
                using (var conn = await _db.ConnectAsync())
                {
                    var report = new VTECReports.Reports(_db);
                    var dataSet = report.Shop_Info(staffId, conn);
                    var shopList = new List<object>();
                    foreach (DataRow row in dataSet.Tables["ShopData"].Rows)
                    {
                        var shopId = row.GetValue<int>("ShopID");
                        if (shopId > 0)
                            shopIds += shopId + ",";
                    }
                    if (shopIds.EndsWith(","))
                        shopIds = shopIds.Substring(0, shopIds.Length - 1);
                }
            }
            return shopIds;
        }

        string ToISODate(DateTime date)
        {
            return string.Format(CultureInfo.InvariantCulture, "'{0:yyyy-MM-dd}'", date);
        }
    }
}