using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;
using VerticalTec.POS.WebService.DataSync.Models;
using vtecPOS_SQL.POSControl;

namespace VerticalTec.POS.WebService.DataSync.Controllers
{
    public class InventoryController : ApiController
    {
        const string LogPrefix = "Inv_";

        IDatabase _database;
        POSModule _posModule;

        public InventoryController(IDatabase database, POSModule posModule)
        {
            _database = database;
            _posModule = posModule;
        }

        [HttpPost]
        [Route("v1/inv/exchange")]
        public async Task<IHttpActionResult> GetExchangeInvenDataAsync(List<int> shopIds)
        {
            var result = new HttpActionResult<IEnumerable<object>>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    List<object> exchangeJsons = new List<object>();
                    foreach (var shopId in shopIds)
                    {
                        var responseText = "";
                        var docJson = "";
                        var ds = new DataSet();
                        var isSuccess = _posModule.ExchangeInventData(ref responseText, ref docJson, ref ds, shopId, conn as SqlConnection);
                        if (isSuccess)
                        {
                            exchangeJsons.Add(new
                            {
                                ShopId = shopId,
                                ExchInvJson = docJson
                            });
                        }
                    }
                    if (exchangeJsons.Count > 0)
                    {
                        result.StatusCode = HttpStatusCode.OK;
                        result.Data = exchangeJsons;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.NotFound;
                        result.Message = "No exchange inventory data";
                    }
                }
            }
            catch (Exception ex)
            {
                var message = $"An error occured {ex.Message}";
                await LogManager.Instance.WriteLogAsync(message);
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = message;
            }
            return result;
        }

        [HttpPost]
        [Route("v1/inv/import")]
        public IHttpActionResult ImportInventoryData(int shopId, [FromBody] object payload)
        {
            var result = new HttpActionResult<object>(Request);
            if (payload == null)
            {
                var msg = $"Very large JSON or invalid format!";
                LogManager.Instance.WriteLog(msg, LogPrefix);
                result.StatusCode = HttpStatusCode.BadRequest;
                result.Message = msg;
                return result;
            }

            using (var conn = _database.Connect())
            {
                var json = JsonConvert.SerializeObject(payload);
                LogManager.Instance.WriteLog($"Begin import inventory {shopId} => {json}", LogPrefix);

                var respText = "";
                var importJson = "";
                var dataSet = new DataSet();
                var success = _posModule.ImportInventData(ref importJson, ref respText, dataSet, json, conn as SqlConnection);

                if (success)
                {
                    var exchInvJson = "";
                    _posModule.ExchangeInventData(ref respText, ref exchInvJson, ref dataSet, shopId, conn as SqlConnection);
                    var body = new
                    {
                        SyncLogJson = importJson,
                        ExchInvJson = exchInvJson
                    };
                    result.Success = success;
                    result.StatusCode = HttpStatusCode.Created;
                    result.Data = body;
                    LogManager.Instance.WriteLog($"Import inventory data successfully", LogPrefix);
                }
                else
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = respText;

                    LogManager.Instance.WriteLog($"Import inventory data {respText}", LogPrefix, LogManager.LogTypes.Error);
                }
            }
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            GC.Collect();
        }
    }
}
