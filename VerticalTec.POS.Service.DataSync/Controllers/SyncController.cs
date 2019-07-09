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
using VerticalTec.POS.Service.DataSync.Models;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync.Controllers
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
        public async Task<IHttpActionResult> SyncInvAsync(string docDate, int shopId)
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
                        var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
                        httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                        httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

                        var content = new StringContent(exportJson, Encoding.UTF8);
                        var respMessage = await httpClient.PostAsync("", content);
                        var respContent = await respMessage.Content.ReadAsStringAsync();
                        var respBody = new ResponseBody<string>();
                        try
                        {
                            respBody = await Task.Run(() => JsonConvert.DeserializeObject<ResponseBody<string>>(respContent));
                        }
                        catch (Exception) { }
                        if (respMessage.IsSuccessStatusCode)
                        {
                            var syncJson = respBody.Data;
                            success = _posModule.SyncInventUpdate(ref respText, syncJson, conn as MySqlConnection);
                            if (!success)
                            {

                            }
                        }
                        else
                        {
                            result.StatusCode = respMessage.StatusCode;
                            result.Message = respBody.Message;
                        }
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
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
