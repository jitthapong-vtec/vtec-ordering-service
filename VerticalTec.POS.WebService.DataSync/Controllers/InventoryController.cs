using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        [Route("v1/inv/import")]
        public async Task<IHttpActionResult> ImportInventoryDataAsync([FromBody]object payload)
        {
            var result = new HttpActionResult<string>(Request);
            if (payload == null)
            {
                var msg = $"Very large JSON or invalid format!";
                await LogManager.Instance.WriteLogAsync(msg, LogPrefix);
                result.StatusCode = HttpStatusCode.BadRequest;
                result.Message = msg;
                return result;
            }
            try
            {
                await LogManager.Instance.WriteLogAsync($"Incoming inventory import data {JsonConvert.SerializeObject(payload, formatting: Formatting.None)}", LogPrefix);
            }
            catch (Exception ex)
            {
                await LogManager.Instance.WriteLogAsync($"Invalid json format of inventory data {ex.Message}", LogPrefix, LogManager.LogTypes.Error);
            }
            using (var conn = await _database.ConnectAsync())
            {
                var respText = "";
                var importJson = "";
                var dataSet = new DataSet();
                var success = _posModule.ImportInventData(ref importJson, ref respText, dataSet, payload.ToString(), conn as SqlConnection);
                if (success)
                {
                    result.Success = success;
                    result.StatusCode = HttpStatusCode.Created;
                    result.Data = importJson;

                    await LogManager.Instance.WriteLogAsync("Import inventory data successfully", LogPrefix);
                }
                else
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = respText;

                    await LogManager.Instance.WriteLogAsync($"Import inventory data {respText}", LogPrefix, LogManager.LogTypes.Error);
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
