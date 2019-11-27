using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin.Models;
using VerticalTec.POS.Service.DataSync.Owin.Utils;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync.Owin.Services
{
    public class DataSyncService : IDataSyncService
    {
        const string LogPrefix = "Inv_";

        IDatabase _db;
        POSModule _posModule;

        public DataSyncService(IDatabase db, POSModule posModule)
        {
            _db = db;
            _posModule = posModule;
        }

        // exportType 0 = default, 1 = end stock, 2 = end stock from counting
        public async Task SyncInvData(IDbConnection conn, int shopId, string startDate, string endDate, string batchUuid = "", int exportType = 0)
        {
            var alreadyHaveTable = await Helper.IsTableExists(_db, conn, Constants.TAB_LOG_FAILURE_SYNC_INV);
            var cmd = _db.CreateCommand(conn);

            if (!alreadyHaveTable)
            {
                cmd.CommandText = "CREATE TABLE " + Constants.TAB_LOG_FAILURE_SYNC_INV + "(" +
                    "StartDate DATETIME NOT NULL," +
                    "EndDate DATETIME NOT NULL," +
                    "BatchUUID CHAR(36), " +
                    "ShopID INT(11) NOT NULL DEFAULT 0," +
                    "ExportType TINYINT(2) NOT NULL DEFAULT 0," +
                    "RetryCounter INT NOT NULL DEFAULT 0," +
                    "FailureText TEXT," +
                    "InsertDate DateTime," +
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

                var success = _posModule.ExportInventData(ref respText, ref dataSet, ref exportJson, exportType, startDate, shopId,
                    documentId, keyShopId, merchantId, brandId, conn as MySqlConnection);
                if (success)
                {
                    var byteCount = 0;
                    try
                    {
                        byteCount = Encoding.UTF8.GetByteCount(exportJson);
                    }
                    catch (Exception) { }
                    await LogManager.Instance.WriteLogAsync($"Export inven data of shop {shopId} {byteCount} bytes.", LogPrefix);

                    if (string.IsNullOrEmpty(batchUuid))
                        batchUuid = dataSet.Tables["Log_BatchExport"].Rows[0]["A"].ToString();

                    exportDatas.Add(new ExportInvenData()
                    {
                        BatchUuid = batchUuid,
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
                    var respText = "";
                    try
                    {
                        await LogManager.Instance.WriteLogAsync($"Begin send inven data of shopId {export.ShopId} to hq", LogPrefix);

                        var exchInvData = await HttpClientManager.Instance.VDSPostAsync<InvExchangeData>($"{importApiUrl}?shopId={export.ShopId}", export.Json);
                        var success = _posModule.SyncInventUpdate(ref respText, exchInvData.SyncLogJson, conn as MySqlConnection);
                        if (success)
                        {
                            await LogManager.Instance.WriteLogAsync($"Sync inven data successfully", LogPrefix);
                        }

                        if (!string.IsNullOrEmpty(exchInvData.ExchInvJson))
                        {
                            if (!_posModule.ImportDocumentData(ref respText, exchInvData.ExchInvJson, conn as MySqlConnection))
                                await LogManager.Instance.WriteLogAsync($"Fail!! ImportDocumentData => {respText}", LogPrefix, LogManager.LogTypes.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is HttpRequestException)
                        {
                            var reqEx = (ex as HttpRequestException);
                            respText = $"{reqEx.InnerException.Message} {vdsUrl}";
                        }
                        else if (ex is HttpResponseException)
                        {
                            var respEx = (ex as HttpResponseException);
                            respText = $"{(ex as HttpResponseException).Response.ReasonPhrase}";
                        }
                        else if (ex is TaskCanceledException)
                        {
                            respText = $"Connection timeout from {vdsUrl}";
                        }
                        else
                        {
                            respText = ex.Message;
                        }

                        var dtLog = new DataTable();
                        cmd.CommandText = $"select * from {Constants.TAB_LOG_FAILURE_SYNC_INV} where StartDate=@startDate and EndDate=@endDate";
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(_db.CreateParameter("@startDate", export.StartDate.Replace("'", "")));
                        cmd.Parameters.Add(_db.CreateParameter("@endDate", export.EndDate.Replace("'", "")));
                        using (var reader = await _db.ExecuteReaderAsync(cmd))
                        {
                            dtLog.Load(reader);
                        }

                        cmd.Parameters.Clear();
                        if (dtLog.Rows.Count == 0)
                        {
                            cmd.CommandText = $"insert into {Constants.TAB_LOG_FAILURE_SYNC_INV} (StartDate, EndDate, BatchUUID, ShopID, ExportType, FailureText, InsertDate)" +
                                $" value (@startDate, @endDate, @batchUuid, @shopId, @exportType, @failureTxt, @insertDate)";
                            cmd.Parameters.Add(_db.CreateParameter("@startDate", export.StartDate.Replace("'", "")));
                            cmd.Parameters.Add(_db.CreateParameter("@endDate", export.EndDate.Replace("'", "")));
                            cmd.Parameters.Add(_db.CreateParameter("@batchUuid", export.BatchUuid));
                            cmd.Parameters.Add(_db.CreateParameter("@shopId", export.ShopId));
                            cmd.Parameters.Add(_db.CreateParameter("@exportType", export.ExportType));
                            cmd.Parameters.Add(_db.CreateParameter("@failureTxt", respText));
                            cmd.Parameters.Add(_db.CreateParameter("@insertDate", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                            try
                            {
                                await _db.ExecuteNonQueryAsync(cmd);
                            }
                            catch (Exception ex1)
                            {

                            }
                        }
                        else
                        {
                            var isCanceled = dtLog.Rows[0].GetValue<int>("IsCanceled");
                            if (isCanceled == 0)
                            {
                                var retryCounter = dtLog.Rows[0].GetValue<int>("RetryCounter");
                                if (++retryCounter > 3)
                                {
                                    cmd.CommandText = $"update {Constants.TAB_LOG_FAILURE_SYNC_INV} set IsCanceled=1";
                                }
                                else
                                {
                                    cmd.CommandText = $"update {Constants.TAB_LOG_FAILURE_SYNC_INV} set RetryCounter=@retryCounter";
                                    cmd.Parameters.Add(_db.CreateParameter("@retryCounter", retryCounter));
                                }
                                cmd.CommandText += ",BatchUUID=@batchUuid,FailureText=@failureTxt where StartDate=@startDate and EndDate=@endDate";
                                cmd.Parameters.Add(_db.CreateParameter("@batchUuid", batchUuid));
                                cmd.Parameters.Add(_db.CreateParameter("@startDate", export.StartDate.Replace("'", "")));
                                cmd.Parameters.Add(_db.CreateParameter("@endDate", export.EndDate.Replace("'", "")));
                                cmd.Parameters.Add(_db.CreateParameter("@failureTxt", respText));
                                await _db.ExecuteNonQueryAsync(cmd);
                            }
                        }
                        await LogManager.Instance.WriteLogAsync($"Fail Send inventory data {respText}", LogPrefix, LogManager.LogTypes.Error);
                    }
                }
            }
        }

        public Task SyncSaleData()
        {
            throw new NotImplementedException();
        }
    }
}
