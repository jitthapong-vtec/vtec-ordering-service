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

namespace VerticalTec.POS.Report.Dashboard.Controllers
{
    [Produces("application/json")]
    public class ReportController : Controller
    {
        IDbHelper _db;

        public ReportController(IDbHelper db)
        {
            _db = db;
        }

        [HttpPost]
        [ActionName("Login")]
        public async Task<IActionResult> LoginAsync(UserLogin payload)
        {
            var result = new ReportActionResult<IEnumerable<object>>();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var cmd = _db.CreateCommand("select StaffID from staffs where StaffCode=@userName and StaffPassword=@password", conn);
                    cmd.Parameters.Add(_db.CreateParameter("@userName", payload.Username ?? ""));
                    cmd.Parameters.Add(_db.CreateParameter("@password", HashUtil.SHA1(payload.Password ?? "")));

                    var dtStaff = new DataTable();
                    using (var reader = await _db.ExecuteReaderAsync(cmd))
                    {
                        dtStaff.Load(reader);
                    }
                    if (dtStaff.Rows.Count > 0)
                    {
                        var staffId = dtStaff.Rows[0].GetValue<int>("StaffID");

                        TempData["StaffID"] = staffId;
                    }
                    else
                    {
                        result.StatusCode = StatusCodes.Status401Unauthorized;
                        result.Message = $"Login fail for {payload.Username}";
                    }

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
        [ActionName("Logout")]
        public IActionResult Logout()
        {
            TempData.Remove("StaffID");
            return Ok();
        }

        [HttpGet]
        [ActionName("ShopData")]
        public async Task<IActionResult> GetShopAsync(int staffId = 2)
        {
            var result = new ReportActionResult<IEnumerable<object>>();
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
                    result.Data = shopList;
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
        [ActionName("Summary")]
        public async Task<IActionResult> GetSummaryReportAsync(string shopIds, DateTime startDate, DateTime endDate)
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
                    var ds = report.Report_SummarySales(shopIds, fromDateStr, toDateStr, 1, cate, conn);
                    foreach (DataRow html in ds.Tables["htmlData"].Rows)
                    {
                        reportHtml.Append(html.GetValue<string>("HtmlData") + "</br>");
                    }
                }
                result.Data = reportHtml.ToString();
            }
            catch (Exception ex)
            {
                result.StatusCode = StatusCodes.Status500InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

        [HttpGet]
        [ActionName("Tender")]
        public async Task<IActionResult> GetTenderReportAsync(string shopIds, DateTime startDate, DateTime endDate)
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
                    var ds = report.Report_TenderData(shopIds, fromDateStr, toDateStr, 1, cate, conn);
                    foreach (DataRow html in ds.Tables["htmlData"].Rows)
                    {
                        reportHtml.Append(html.GetValue<string>("HtmlData") + "</br>");
                    }
                }
                result.Data = reportHtml.ToString();
            }
            catch (Exception ex)
            {
                result.StatusCode = StatusCodes.Status500InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

        [HttpGet]
        [ActionName("Audit")]
        public async Task<IActionResult> GetAuditReportAsync(string shopIds, DateTime startDate, DateTime endDate)
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

                    var ds = report.Report_AuditData(shopIds, fromDateStr, toDateStr, 1, cate, conn);
                    reportHtml.Append(ds.Tables["htmlData"].Select("ReportType='voidData'").FirstOrDefault().GetValue<string>("HtmlData"));
                }
                result.Data = reportHtml.ToString();
            }
            catch (Exception ex)
            {
                result.StatusCode = StatusCodes.Status500InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }

        string ValidateShopIds(string shopIds)
        {
            return string.IsNullOrEmpty(shopIds) ? "" : shopIds;
        }

        string ToISODate(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}