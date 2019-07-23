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

        [HttpGet]
        [ActionName("Summary")]
        public async Task<IActionResult> GetSummaryReportAsync(string shopIds = "", string startDate = "", string endDate = "")
        {
            var result = new ReportActionResult<string>();
            var reportHtml = new StringBuilder();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    CheckParameter(ref shopIds, ref startDate, ref endDate);

                    var cate = new Dictionary<int, string>();

                    var report = new VTECReports.Reports(_db);

                    var ds = report.Report_SummarySales(shopIds, startDate, endDate, 1, cate, conn);
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
        public async Task<IActionResult> GetTenderReportAsync(string shopIds = "", string startDate = "", string endDate = "")
        {
            var result = new ReportActionResult<string>();
            var reportHtml = new StringBuilder();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    CheckParameter(ref shopIds, ref startDate, ref endDate);

                    var cate = new Dictionary<int, string>();

                    var report = new VTECReports.Reports(_db);

                    var ds = report.Report_TenderData(shopIds, startDate, endDate, 1, cate, conn);
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
        public async Task<IActionResult> GetAuditReportAsync(string shopIds = "", string startDate = "", string endDate = "")
        {
            var result = new ReportActionResult<string>();
            var reportHtml = new StringBuilder();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    CheckParameter(ref shopIds, ref startDate, ref endDate);

                    var cate = new Dictionary<int, string>();

                    var report = new VTECReports.Reports(_db);
                    var ds = report.Report_AuditData(shopIds, startDate, endDate, 1, cate, conn);
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

        private static void CheckParameter(ref string shopIds, ref string startDate, ref string endDate)
        {
            if (string.IsNullOrEmpty(shopIds))
                shopIds = "";
            if (string.IsNullOrEmpty(startDate))
                startDate = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(endDate))
                endDate = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}