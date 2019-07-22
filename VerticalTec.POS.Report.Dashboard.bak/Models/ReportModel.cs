using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using vtecdbhelper;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Report.Dashboard.Models
{
    public class ReportModel : PageModel
    {
        IDbHelper _db;

        public ReportModel(IDbHelper db)
        {
            _db = db;
        }

        public string ReportHtml { get; set; }

        public async Task OnGet()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var shopIds = "";
                var startDate = "2019-07-21";
                var endDate = "2019-07-21";
                var cate = new Dictionary<int, string>();

                var report = new VTECReports.Reports(_db);

                var ds = report.Report_SummarySales(shopIds, startDate, endDate, 1, cate, conn);
                foreach (DataRow html in ds.Tables["htmlData"].Rows)
                {
                    ReportHtml += html.GetValue<string>("HtmlData") + "</br>";
                }

                ds = report.Report_TenderData(shopIds, startDate, endDate, 1, cate, conn);
                foreach (DataRow html in ds.Tables["htmlData"].Rows)
                {
                    ReportHtml += html.GetValue<string>("HtmlData") + "</br>";
                }

                ds = report.Report_AuditData(shopIds, startDate, endDate, 1, cate, conn);
                ReportHtml += ds.Tables["htmlData"].Select("ReportType='voidData'").FirstOrDefault().GetValue<string>("HtmlData");
            }
        }
    }
}
