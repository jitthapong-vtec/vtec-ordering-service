using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using vtecdbhelper;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Report.Dashboard.Pages.Public
{
    public class FullTaxReportModel : PageModel
    {
        IDbHelper _db;

        public FullTaxReportModel(IDbHelper db)
        {
            _db = db;
        }

        public string Html { get; set; }

        public async Task OnGet(int id, int compId, int shopId, int langId, int staffId, int terminalId)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var report = new VTECReports.Reports(_db);
                    var ds = report.FullTax(id, compId, shopId, langId, staffId, terminalId, 1, "", conn);
                    Html = ds?.Tables["htmlData"].Select("ReportType='FullTaxData'").FirstOrDefault()?.GetValue<string>("HtmlData");
                }
            }
            catch (Exception ex)
            {
                Html = ex.Message;
            }
        }
    }
}