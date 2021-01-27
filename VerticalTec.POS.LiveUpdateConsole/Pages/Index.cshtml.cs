using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.LiveUpdateConsole.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly LiveUpdateDbContext _liveUpdateCtx;
        private readonly IDatabase _db;

        public IndexModel(IDatabase db, LiveUpdateDbContext liveUpdateCtx, ILogger<IndexModel> logger)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetVersionDeployAsync(DataSourceLoadOptions options)
        {
            List<VersionDeploy> versionDeploys = new List<VersionDeploy>();
            using(var conn = await _db.ConnectAsync())
            {
                versionDeploys = await _liveUpdateCtx.GetVersionDeploy(conn);
            }
            return new JsonResult(DataSourceLoader.Load(versionDeploys, options));
        }

        public void OnGet()
        {

        }
    }
}
