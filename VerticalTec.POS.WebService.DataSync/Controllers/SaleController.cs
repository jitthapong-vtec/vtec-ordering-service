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
    public class SaleController : ApiController
    {
        const string LogPrefix = "Sale_";

        IDatabase _database;
        POSModule _posModule;

        public SaleController(IDatabase database, POSModule posModule)
        {
            _database = database;
            _posModule = posModule;
        }

        [HttpPost]
        [Route("v1/sale/import")]
        public async Task<IHttpActionResult> ImportSaleAsync([FromBody] object payload)
        {
            var result = new HttpActionResult<string>(Request);
            if (payload == null)
            {
                var msg = $"Invalid json format!";
                await LogManager.Instance.WriteLogAsync(msg, LogPrefix);
                result.StatusCode = HttpStatusCode.BadRequest;
                result.Message = msg;
                return result;
            }
            try
            {
                await LogManager.Instance.WriteLogAsync($"Incoming sale import data {JsonConvert.SerializeObject(payload, formatting: Formatting.None)}", LogPrefix);
            }
            catch (Exception ex)
            {
                await LogManager.Instance.WriteLogAsync($"Invalid json format of inventory data {ex.Message}", LogPrefix, LogManager.LogTypes.Error);
            }
            using (var conn = await _database.ConnectAsync() as SqlConnection)
            {
                var importJson = "";
                var respText = "";
                var dataSet = new DataSet();
                var success = _posModule.ImportData(ref importJson, ref respText, dataSet, payload.ToString(), conn);
                if (success)
                {
                    result.Success = success;
                    result.StatusCode = HttpStatusCode.Created;
                    result.Data = importJson;

                    await LogManager.Instance.WriteLogAsync("Import sale data successfully", LogPrefix);
                }
                else
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = respText;

                    await LogManager.Instance.WriteLogAsync($"Import sale data {respText}", LogPrefix, LogManager.LogTypes.Error);
                }
            }
            return result;
        }
    }
}
