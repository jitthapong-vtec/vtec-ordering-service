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
                using (var conn = await _db.ConnectAsync() as MySqlConnection)
                {
                    var prop = new ProgramProperty(_db);
                    var vdsUrl = prop.GetVdsUrl(conn);
                    var importApiUrl = $"{vdsUrl}/v1/inv/import";

                    var shopData = new ShopData(_db);
                    var dtShop = await shopData.GetShopDataAsync(conn);
                    var exportDatas = new Dictionary<int, string>();

                    DataRow[] shops;
                    if (shopId == 0)
                        shops = dtShop.Select("IsInv=1");
                    else
                        shops = dtShop.Select($"ShopID={shopId}");

                    if(shops.Length == 0)
                    {
                        result.StatusCode = HttpStatusCode.BadRequest;
                        result.Message = $"No shop data to export";
                        return result;
                    }

                    foreach (var shop in shops)
                    {
                        var respText = "";
                        var exportJson = "";
                        var dataSet = new DataSet();
                        var exportType = 0;
                        var documentId = 0;
                        var keyShopId = 0;
                        var merchantId = shop.GetValue<int>("MerchantID");
                        var brandId = shop.GetValue<int>("BrandID");

                        shopId = shop.GetValue<int>("ShopID");

                        var success = _posModule.ExportInventData(ref respText, ref dataSet, ref exportJson, exportType, docDate, shopId,
                            documentId, keyShopId, merchantId, brandId, conn);
                        if (success)
                        {
                            var byteCount = 0;
                            try
                            {
                                byteCount = Encoding.UTF8.GetByteCount(exportJson);
                            }
                            catch (Exception) { }
                            await LogManager.Instance.WriteLogAsync($"Export inven data of shop {shopId} {byteCount} bytes.", LogPrefix);
                            exportDatas.Add(shopId, exportJson);
                        }
                        else
                        {
                            await LogManager.Instance.WriteLogAsync($"Export inven data error => {respText}", LogPrefix);
                        }
                    }

                    if (exportDatas.Count > 0)
                    {
                        if (timeout > 0)
                            HttpClientManager.Instance.ConnTimeOut = TimeSpan.FromMinutes(timeout);

                        foreach (var export in exportDatas)
                        {
                            try
                            {
                                await LogManager.Instance.WriteLogAsync($"Begin send inven data of shopId {export.Key} to hq", LogPrefix);

                                var respText = "";
                                var exchInvData = await HttpClientManager.Instance.VDSPostAsync<InvExchangeData>($"{importApiUrl}?shopId={export.Key}", export.Value);

                                var success = false;
                                if (exchInvData != null)
                                {
                                    await LogManager.Instance.WriteLogAsync($"ExchInvJson => {exchInvData.ExchInvJson}", LogPrefix);
                                    await LogManager.Instance.WriteLogAsync($"SyncLogJson => {exchInvData.SyncLogJson}", LogPrefix);

                                    success = _posModule.ImportDocumentData(ref respText, exchInvData.ExchInvJson, conn);
                                    success = _posModule.SyncInventUpdate(ref respText, exchInvData.SyncLogJson, conn);
                                }

                                if (success)
                                {
                                    result.Message = $"Sync inven data successfully";
                                    await LogManager.Instance.WriteLogAsync($"Sync inven data successfully", LogPrefix);
                                }
                                else
                                {
                                    result.Message = respText;
                                }
                                result.Success = success;
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
                    }
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
