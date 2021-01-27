using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.LiveUpdateConsole.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class VersionsController : ControllerBase
    {
        private IDatabase _db;
        private LiveUpdateDbContext _liveUpdateCtx;
        private ILogger<VersionsController> _logger;

        public VersionsController(IDatabase db, LiveUpdateDbContext liveUpdateCtx, ILogger<VersionsController> logger)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;
            _logger = logger;
        }

        [HttpGet("Deploy")]
        public async Task<ActionResult<VersionDeploy>> GetDeployVersionAsync(int shopId)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var brandId = 0;
                    var cmd = _db.CreateCommand(conn);
                    cmd.CommandText = "select BrandID from shop_data where ShopID=@shopId";
                    cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
                    using (var reader = await _db.ExecuteReaderAsync(cmd))
                    {
                        if (reader.Read())
                        {
                            brandId = reader.GetValue<int>("BrandID");
                        }
                    }
                    var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn);
                    var versionDeploy = versionsDeploy.Where(v => v.BrandId == brandId && v.BatchStatus == VersionDeployBatchStatus.Actived).SingleOrDefault();
                    return versionDeploy;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDeployVersionAsync");
                return BadRequest();
            }
        }

        [HttpPost("Current")]
        public async Task<ActionResult<VersionInfo>> UpdateCurrentVersionInfo(VersionInfo versionInfo)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    versionInfo.SyncStatus = 1;
                    versionInfo.IsOnline = true;
                    versionInfo.UpdateDate = DateTime.Now;

                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);
                    return versionInfo;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReceiveVersionInfo");
                return BadRequest();
            }
        }

        [HttpPost("Status")]
        public async Task<ActionResult<VersionLiveUpdate>> UpdateVersionStatus(VersionLiveUpdate versionLiveUpdate)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    versionLiveUpdate.SyncStatus = 1;
                    versionLiveUpdate.UpdateDate = DateTime.Now;

                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);
                    return versionLiveUpdate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateVersionStatus");
                return BadRequest();
            }
        }
    }
}
