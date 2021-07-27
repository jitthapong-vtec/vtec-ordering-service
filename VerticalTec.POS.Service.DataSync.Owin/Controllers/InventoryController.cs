using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin.Models;
using vtecPOS.GlobalFunctions;
using VerticalTec.POS.Utils;
using VerticalTec.POS.Service.DataSync.Owin.Utils;
using MySql.Data.MySqlClient;
using System.Globalization;
using VerticalTec.POS.Service.DataSync.Owin.Services;

namespace VerticalTec.POS.Service.DataSync.Owin.Controllers
{
    public class InventoryController : ApiController
    {
        const string LogPrefix = "Inv_";

        IDatabase _db;
        IDataSyncService _dataSyncService;
        POSModule _posModule;

        public InventoryController(IDatabase database, IDataSyncService dataSyncService, POSModule posModule)
        {
            _db = database;
            _dataSyncService = dataSyncService;
            _posModule = posModule;
        }

        [HttpPost]
        [Route("v1/inv/exchange")]
        public async Task<IHttpActionResult> ExchangeInventoryDataAsync(List<int> shopIds)
        {
            await LogManager.Instance.WriteLogAsync($"Call v1/inv/exchange", LogPrefix);
            var result = new HttpActionResult<string>(Request);
            using (var conn = await _db.ConnectAsync() as MySqlConnection)
            {
                var prop = new ProgramProperty(_db);
                var vdsUrl = prop.GetVdsUrl(conn);
                var apiUrl = $"{vdsUrl}/v1/inv/exchange";
                try
                {
                    var exchanges = await HttpClientManager.Instance.VDSPostAsync<List<InvExchangeData>>(apiUrl, shopIds);
                    foreach (var exchange in exchanges)
                    {
                        var responseText = "";
                        var exchInvJson = exchange.ExchInvJson;
                        var shopId = exchange.ShopId;
                        var isSuccess = _posModule.ImportDocumentData(ref responseText, exchInvJson, conn);
                        if (isSuccess)
                        {
                            await LogManager.Instance.WriteLogAsync($"Import document shop {shopId} successfully.", LogPrefix);
                        }
                        else
                        {
                            await LogManager.Instance.WriteLogAsync($"Import document shop {shopId} fail {responseText}", LogPrefix);
                        }
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

        [HttpGet]
        [Route("v2/inv/sendtohq")]
        public async Task<IHttpActionResult> SendInvWithRecoveryWhenFailAsync(int shopId, string batchuuid, string startDate, string endDate, int exportType = 0)
        {
            await LogManager.Instance.WriteLogAsync($"Call v2/inv/sendtohq?shopId={shopId}&batchUuid={batchuuid}&exportType={exportType}&startDate={startDate}&endDate={endDate}", LogPrefix);

            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    await _dataSyncService.SyncInvData(conn, shopId, startDate, endDate, batchuuid, exportType);
                    result.Message = "Ok, received request from you";
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
        [Route("v1/inv/sendtohq")]
        public async Task<IHttpActionResult> SendInvAsync(int shopId = 0, string docDate = "", int timeout = 10)
        {
            await LogManager.Instance.WriteLogAsync($"Call v1/inv/sendtohq?shopId={shopId}&docDate={docDate}", LogPrefix);

            var result = new HttpActionResult<string>(Request);
            try
            {
                using(var conn = await _db.ConnectAsync())
                {
                    result.Success = true;
                    result.Message = await _dataSyncService.SyncInvenData(conn, shopId, docDate, timeout);
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.BadRequest;
                result.Message = ex.Message;
            }
            return result;
        }
    }
}
