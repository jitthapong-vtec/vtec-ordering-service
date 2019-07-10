using MySql.Data.MySqlClient;
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

namespace VerticalTec.POS.Service.DataSync.Owin.Controllers
{
    public class SyncController : ApiController
    {
        IDatabase _database;
        POSModule _posModule;

        public SyncController(IDatabase database, POSModule posModule)
        {
            _database = database;
            _posModule = posModule;
        }

        [HttpGet]
        [Route("v1/sync/inv")]
        public async Task<IHttpActionResult> SyncInvAsync(int shopId = 0, string docDate = "")
        {
            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var respText = "";
                    var exportJson = "";
                    var dataSet = new DataSet();
                    var exportType = 100;
                    var documentId = 0;
                    var keyShopId = 0;
                    var merchantId = 0;
                    var brandId = 0;

                    var success = _posModule.ExportInventData(ref respText, ref dataSet, ref exportJson, exportType, docDate, shopId,
                        documentId, keyShopId, merchantId, brandId, conn as MySqlConnection);
                    if (success)
                    {
                        var url = "http://localhost/syncapi/v1/import/inv";
                        try
                        {
                            var syncJson = await HttpClientManager.Instance.PostAsync<string>(url, exportJson);
                            success = _posModule.SyncInventUpdate(ref respText, syncJson, conn as MySqlConnection);
                            result.Success = success;
                            if (success)
                            {
                                result.Message = "Sync inventory data successfully";
                            }
                            else
                            {
                                result.Message = respText;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.StatusCode = HttpStatusCode.RequestTimeout;
                            if (ex is HttpRequestException)
                                result.Message = $"Connecton timeout {url}";
                            else if (ex is HttpResponseException)
                                result.Message = (ex as HttpResponseException).Response.ReasonPhrase;
                            else
                                result.Message = ex.Message;
                        }
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.OK;
                        result.Message = respText;
                    }
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }
    }
}
