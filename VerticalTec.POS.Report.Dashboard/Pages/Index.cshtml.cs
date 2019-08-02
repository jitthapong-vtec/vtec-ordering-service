using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VerticalTec.POS.Report.Dashboard.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet(string staffId = "")
        {
            if (string.IsNullOrEmpty(staffId))
                return Redirect(Url.Page("Login"));
            else
                return Page();
        }
    }
}