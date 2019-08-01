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

namespace VerticalTec.POS.Service.DataSync.Owin.Controllers
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

        [HttpGet]
        [Route("v1/inv/sendtohq")]
        public async Task<IHttpActionResult> SendInvAsync(int shopId = 0, string docDate = "")
        {
            await LogManager.Instance.WriteLogAsync($"Call v1/inv/sendtohq?shopId={shopId}&docDate={docDate}", LogPrefix);

            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync() as MySqlConnection)
                {
                    var prop = new ProgramProperty(_database);
                    var vdsUrl = "";
                    try
                    {
                        vdsUrl = prop.GetVdsUrl(conn);
                    }
                    catch (Exception)
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = "vdsurl parameter in property 1050 is not set or did not enabled this property";
                        await LogManager.Instance.WriteLogAsync($"{result.Message}", LogPrefix, LogManager.LogTypes.Error);
                        return result;
                    }

                    var importApiUrl = $"{vdsUrl}/v1/inv/import";
                    var respText = "";
                    var exportJson = "";
                    var dataSet = new DataSet();
                    var exportType = 100;
                    var documentId = 0;
                    var keyShopId = 0;
                    var shopData = new ShopData(_database);
                    var merchantId = 0;
                    var brandId = 0;
                    try
                    {
                        var shop = await shopData.GetShopDataAsync(conn, shopId);
                        merchantId = shop.GetValue<int>("MerchantID");
                        brandId = shop.GetValue<int>("BrandID");
                    }
                    catch (Exception) { }

                    var success = _posModule.ExportInventData(ref respText, ref dataSet, ref exportJson, exportType, docDate, shopId,
                        documentId, keyShopId, merchantId, brandId, conn);
                    if (success)
                    {
                        await LogManager.Instance.WriteLogAsync($"Export inven data => {exportJson}", LogPrefix);

                        try
                        {
                            await LogManager.Instance.WriteLogAsync($"Begin send inven data to hq", LogPrefix);

                            var syncJson = await HttpClientManager.Instance.VDSPostAsync<string>(importApiUrl, exportJson);
                            success = _posModule.SyncInventUpdate(ref respText, syncJson, conn);
                            result.Success = success;
                            if (success)
                            {
                                result.Message = $"Send inventory data successfully";

                                await LogManager.Instance.WriteLogAsync($"Import inven data at hq successfully", LogPrefix);
                            }
                            else
                            {
                                result.Message = respText;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex is HttpRequestException)
                            {
                                var reqEx = (ex as HttpRequestException);
                                result.StatusCode = HttpStatusCode.RequestTimeout;
                                result.Message = $"{reqEx.InnerException.Message} {vdsUrl}";
                            }
                            else if (ex is HttpResponseException)
                            {
                                var respEx = (ex as HttpResponseException);
                                result.StatusCode = respEx.Response.StatusCode;
                                result.Message = $"{(ex as HttpResponseException).Response.ReasonPhrase}";
                            }
                            else
                            {
                                result.Message = ex.Message;
                            }
                            await LogManager.Instance.WriteLogAsync($"Send inventory data fail {result.Message}", LogPrefix, LogManager.LogTypes.Error);
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
