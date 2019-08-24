using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using vtecdbhelper;
using VerticalTec.POS.Utils;
using System.Globalization;
using VerticalTec.POS.Report.Dashboard.Models;
using DevExtreme.AspNet.Mvc;
using VerticalTec.POS.Database;

namespace VerticalTec.POS.Report.Dashboard.Controllers
{
    public class ReportController : ApiControllerBase
    {
        IDbHelper _db;
        IDatabase _db2;

        public ReportController(IDbHelper db, IDatabase db2)
        {
            _db = db;
            _db2 = db2;
        }

        [HttpGet]
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
        public async Task<IActionResult> GetBillReportAsync(string shopIds, DateTime startDate, DateTime endDate, int reportType = 0, int langId = 1)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var report = new VTECReports.Reports(_db);
                    var posRepo = new VtecRepo(_db2);

                    var cate = new Dictionary<int, string>();
                    shopIds = ValidateShopIds(shopIds);
                    var fromDateStr = ToISODate(startDate);
                    var toDateStr = ToISODate(endDate);

                    var prop = await posRepo.GetProgramPropertyAsync(conn);
                    var dateFormat = prop.Select($"PropertyID = 13").FirstOrDefault()?.GetValue<string>("PropertyTextValue");
                    var ds = report.Report_BillData(shopIds, fromDateStr, toDateStr, reportType, langId, cate, conn);

                    var bills = new List<object>();
                    foreach (DataRow row in ds.Tables["BillData"].Rows)
                    {
                        bills.Add(new
                        {
                            ShopName = row.GetValue<string>("ShopName"),
                            SaleDate = row.GetValue<DateTime>("SaleDate").ToString(dateFormat),
                            ReceiptNumber = row.GetValue<string>("ReceiptNumber"),
                            TransactionStatus = row.GetValue<int>("TransactionStatusID"),
                            Qty = row.GetValue<decimal>("ReceiptTotalQty"),
                            Amount = row.GetValue<decimal>("ReceiptPayPrice")
                        });
                    }
                    return Ok(bills);
                }
            }
            catch (Exception ex)
            {
                return NoContent();
            }
        }

        [HttpGet]
        [ActionName("hourly")]
        public async Task<IActionResult> GetHourlyReport(string shopIds, DateTime startDate, DateTime endDate, int reportType = 0, int langId = 1)
        {
            var result = new ReportActionResult<object>();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var report = new VTECReports.Reports(_db);
                    var posRepo = new VtecRepo(_db2);

                    var cate = new Dictionary<int, string>();

                    shopIds = ValidateShopIds(shopIds);
                    var fromDateStr = ToISODate(startDate);
                    var toDateStr = ToISODate(endDate);
                    var prop = await posRepo.GetProgramPropertyAsync(conn, 12);
                    var currencyFormat = prop.Rows[0].GetValue<string>("PropertyTextValue");

                    var ds = report.Report_HourlyData(shopIds, fromDateStr, toDateStr, reportType, langId, cate, conn);
                    result.Data = new
                    {
                        raw = ds.Tables["HourlyData"],
                        stat = new
                        {
                            Summary = ds.Tables["StatData"].Rows[0].GetValue<decimal>("DataValue").ToString(currencyFormat),
                            AvgPerHour = ds.Tables["StatData"].Rows[1].GetValue<decimal>("DataValue").ToString(currencyFormat)
                        },
                        chartData = ds.Tables["GraphData"],
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
        public async Task<IActionResult> GetSummaryReportAsync(string shopIds, DateTime startDate, DateTime endDate, int langId = 1)
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

                    shopIds = ValidateShopIds(shopIds);
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
                    productSaleChartData = ds.Tables["ProductCatGraphData"],
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
        public async Task<IActionResult> GetTenderReportAsync(string shopIds, DateTime startDate, DateTime endDate, int langId = 1)
        {
            var result = new ReportActionResult<string>();
            var reportHtml = new StringBuilder();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var cate = new Dictionary<int, string>();
                    var report = new VTECReports.Reports(_db);

                    shopIds = ValidateShopIds(shopIds);
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
        public async Task<IActionResult> GetAuditReportAsync(string shopIds, DateTime startDate, DateTime endDate, int langId = 1)
        {
            var result = new ReportActionResult<string>();
            var reportHtml = new StringBuilder();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var cate = new Dictionary<int, string>();
                    var report = new VTECReports.Reports(_db);

                    shopIds = ValidateShopIds(shopIds);
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

        string ValidateShopIds(string shopIds)
        {
            if (string.IsNullOrEmpty(shopIds))
                shopIds = "";
            else if (shopIds == "0")
                shopIds = "";
            else if (shopIds == "null")
                shopIds = "";
            return shopIds;
        }

        string ToISODate(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}