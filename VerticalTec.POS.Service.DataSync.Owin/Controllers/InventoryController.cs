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

namespace VerticalTec.POS.Service.DataSync.Owin.Controllers
{
    public class InventoryController : ApiController
    {
        const string LogPrefix = "Inv_";

        IDatabase _db;
        POSModule _posModule;

        public InventoryController(IDatabase database, POSModule posModule)
        {
            _db = database;
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
            // exportType 0 = default, 1 = end stock, 2 = end stock from counting
            await LogManager.Instance.WriteLogAsync($"Call v2/inv/sendtohq?shopId={shopId}&batchUuid={batchuuid}&exportType={exportType}&startDate={startDate}&endDate={endDate}", LogPrefix);

            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _db.ConnectAsync() as MySqlConnection)
                {
                    var tableName = "log_vds_failure_sync_inv";
                    var alreadyHaveTable = await Helper.IsTableExists(_db, conn, tableName);
                    var cmd = _db.CreateCommand(conn);

                    if (!alreadyHaveTable)
                    {
                        cmd.CommandText = "CREATE TABLE " + tableName + "(" +
                            "StartDate DATETIME NOT NULL," +
                            "EndDate DATETIME NOT NULL," +
                            "BatchUUID CHAR(36), " +
                            "ShopID INT(11) NOT NULL DEFAULT 0," +
                            "ExportType TINYINT(1) NOT NULL DEFAULT 0," +
                            "OnSchedule TINYINT(1) NOT NULL DEFAULT 0," +
                            "TotalRetry TINYINT(1) NOT NULL DEFAULT 3," +
                            "FailureText TEXT," +
                            "IsCanceled TINYINT(1) NOT NULL DEFAULT 0," +
                            "PRIMARY KEY(StartDate, EndDate)" +
                            ") ENGINE = INNODB; ";
                        await _db.ExecuteNonQueryAsync(cmd);
                    }

                    var prop = new ProgramProperty(_db);
                    var vdsUrl = prop.GetVdsUrl(conn);
                    var importApiUrl = $"{vdsUrl}/v1/inv/import";

                    var shopData = new ShopData(_db);
                    var dtShop = await shopData.GetShopDataAsync(conn);
                    var exportDatas = new List<ExportInvenData>();

                    var shopRows = dtShop.Select("IsInv=1");
                    if (shopId > 0)
                        shopRows = dtShop.Select($"ShopID={shopId}");

                    if (!startDate.Contains("'"))
                        startDate = "'" + startDate + "'";
                    if (!endDate.Contains("'"))
                        endDate = "'" + endDate + "'";

                    foreach (var shop in shopRows)
                    {
                        var respText = "";
                        var exportJson = "";
                        var dataSet = new DataSet();
                        var documentId = 0;
                        var keyShopId = 0;
                        var merchantId = shop.GetValue<int>("MerchantID");
                        var brandId = shop.GetValue<int>("BrandID");
                        shopId = shop.GetValue<int>("ShopID");

                        //HttpClientManager.Instance.ConnTimeOut = TimeSpan.FromSeconds(3);
                        //_posModule.DocumentResetSync(ref respText, shopId, startDate, endDate, conn as MySqlConnection);

                        var success = _posModule.ExportInventData(ref respText, ref dataSet, ref exportJson, exportType, startDate, shopId,
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

                            if (string.IsNullOrEmpty(batchuuid))
                                batchuuid = dataSet.Tables["Log_BatchExport"].Rows[0]["A"].ToString();

                            exportDatas.Add(new ExportInvenData()
                            {
                                BatchUuid = batchuuid,
                                ShopId = shopId,
                                ExportType = exportType,
                                StartDate = startDate,
                                EndDate = endDate,
                                Json = exportJson
                            });
                        }
                        else
                        {
                            await LogManager.Instance.WriteLogAsync($"Export inven data error => {respText}", LogPrefix);
                        }
                    }

                    if (exportDatas.Count > 0)
                    {
                        foreach (var export in exportDatas)
                        {
                            try
                            {
                                await LogManager.Instance.WriteLogAsync($"Begin send inven data of shopId {export.ShopId} to hq", LogPrefix);

                                var respText = "";
                                var exchInvData = await HttpClientManager.Instance.VDSPostAsync<InvExchangeData>($"{importApiUrl}?shopId={export.ShopId}", export.Json);
                                var success = _posModule.SyncInventUpdate(ref respText, exchInvData.SyncLogJson, conn);
                                if (success)
                                {
                                    result.Message = $"Sync inven data successfully";
                                    await LogManager.Instance.WriteLogAsync($"Sync inven data successfully", LogPrefix);
                                }
                                else
                                {
                                    result.Message = respText;
                                }

                                if (!string.IsNullOrEmpty(exchInvData.ExchInvJson))
                                {
                                    if(!_posModule.ImportDocumentData(ref respText, exchInvData.ExchInvJson, conn))
                                        await LogManager.Instance.WriteLogAsync($"Fail!! ImportDocumentData => {result.Message}", LogPrefix, LogManager.LogTypes.Error);
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
                                else if(ex is TaskCanceledException)
                                {
                                    result.StatusCode = HttpStatusCode.RequestTimeout;
                                    result.Message = $"Connection timeout from {vdsUrl}";
                                }
                                else
                                {
                                    result.Message = ex.Message;
                                }

                                cmd.CommandText = $"insert into {tableName} (BatchUUID, ShopID, ExportType, OnSchedule, StartDate, EndDate, FailureText)" +
                                    $" value (@batchUuid, @shopId, @exportType, @onSchedule, @startDate, @endDate, @failureTxt)";
                                cmd.Parameters.Clear();
                                cmd.Parameters.Add(_db.CreateParameter("@batchUuid", export.BatchUuid));
                                cmd.Parameters.Add(_db.CreateParameter("@shopId", export.ShopId));
                                cmd.Parameters.Add(_db.CreateParameter("@exportType", export.ExportType));
                                cmd.Parameters.Add(_db.CreateParameter("@onSchedule", 1));
                                cmd.Parameters.Add(_db.CreateParameter("@startDate", export.StartDate.Replace("'", "")));
                                cmd.Parameters.Add(_db.CreateParameter("@endDate", export.EndDate.Replace("'", "")));
                                cmd.Parameters.Add(_db.CreateParameter("@failureTxt", result.Message));
                                try
                                {
                                    await _db.ExecuteNonQueryAsync(cmd);
                                }
                                catch (Exception) { }
                                await LogManager.Instance.WriteLogAsync($"Fail Send inventory data {result.Message}", LogPrefix, LogManager.LogTypes.Error);
                            }
                        }
                    }
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
        public async Task<IHttpActionResult> SendInvAsync(int shopId = 0, string docDate = "")
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
                    foreach (var shop in dtShop.Select($"IsInv=1"))
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
                        foreach (var export in exportDatas)
                        {
                            try
                            {
                                await LogManager.Instance.WriteLogAsync($"Begin send inven data of shopId {export.Key} to hq", LogPrefix);

                                var respText = "";
                                var exchInvData = await HttpClientManager.Instance.VDSPostAsync<InvExchangeData>($"{importApiUrl}?shopId={export.Key}", export.Value);
                                var success = _posModule.ImportDocumentData(ref respText, exchInvData.ExchInvJson, conn);
                                success = _posModule.SyncInventUpdate(ref respText, exchInvData.SyncLogJson, conn);
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
                result.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }
    }
}
