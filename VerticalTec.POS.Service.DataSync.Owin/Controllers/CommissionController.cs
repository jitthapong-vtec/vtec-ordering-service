using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin.Models;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.DataSync.Owin.Controllers
{
    public class CommissionController : ApiController
    {
        const string LogPrefix = "Commission_";

        IDatabase _db;

        public CommissionController(IDatabase db)
        {
            _db = db;
        }

        [HttpGet]
        [Route("v1/commission/sendreceipt")]
        public async Task<IHttpActionResult> SendReceiptCommissionAsync(int shopId, int tranId, int compId)
        {
            await LogManager.Instance.WriteLogAsync($"Call v1/commission?tranId={tranId}&compId={compId}", LogPrefix);

            var result = new HttpActionResult<string>(Request);
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var prop = new ProgramProperty(_db);
                    var commissionApi = "";
                    try
                    {
                        commissionApi = prop.GetCommissionApiUrl(conn);
                        if (!commissionApi.EndsWith("/"))
                            commissionApi = commissionApi + "/";
                    }
                    catch (Exception)
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = "CommissionApi parameter in property 2003 is not set or did not enabled this property";
                        await LogManager.Instance.WriteLogAsync($"{result.Message}", LogPrefix, LogManager.LogTypes.Error);
                        return result;
                    }

                    var commissionUrl = $"{commissionApi}";
                    var tableName = "log_commissionsync";
                    var alreadyHaveTable = false;

                    var cmd = _db.CreateCommand(conn);
                    cmd.CommandText = "SELECT * FROM information_schema.tables WHERE table_schema = @dbName " +
                        "AND TABLE_NAME = @tableName LIMIT 1; ";
                    cmd.Parameters.Add(_db.CreateParameter("@dbName", GlobalVar.Instance.DbName));
                    cmd.Parameters.Add(_db.CreateParameter("@tableName", tableName));
                    using (var reader = await _db.ExecuteReaderAsync(cmd))
                    {
                        if (reader.Read())
                        {
                            alreadyHaveTable = true;
                        }
                    }

                    if (!alreadyHaveTable)
                    {
                        cmd.CommandText = "CREATE TABLE " + tableName + "(" +
                            "TaskID CHAR(36)," +
                            "TransactionID INT(11)," +
                            "ComputerID INT(11)," +
                            "ShopID INT(11)," +
                            "SyncStartTime DATETIME," +
                            "SyncEndTime DATETIME," +
                            "SaleDate DATETIME," +
                            "SyncStatus TINYINT(4) NOT NULL Default 0," +
                            "Message VARCHAR(255)," +
                            "PRIMARY KEY(TransactionID, ComputerID)" +
                            ") ENGINE = INNODB; ";
                        cmd.Parameters.Clear();
                        await _db.ExecuteNonQueryAsync(cmd);
                    }

                    cmd.CommandText = "delete from " + tableName + " where SyncStatus=1 and SaleDate <= @outDate";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(_db.CreateParameter("@outDate", DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                    await _db.ExecuteNonQueryAsync(cmd);

                    try
                    {
                        cmd.CommandText = "insert into " + tableName + "(TaskID, TransactionID, ComputerID, ShopID) values(@taskId, @tranId, @compId, @shopId)";
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(_db.CreateParameter("@taskId", Guid.NewGuid().ToString()));
                        cmd.Parameters.Add(_db.CreateParameter("@tranId", tranId));
                        cmd.Parameters.Add(_db.CreateParameter("@compId", compId));
                        cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
                        await _db.ExecuteNonQueryAsync(cmd);
                    }
                    catch (Exception) { }

                    cmd.CommandText = "select a.TaskID, b.SaleDate, b.ReceiptNumber, b.TransactionNote, " +
                        " b.ReceiptRetailPrice, b.TransactionVAT, b.ReceiptPayPrice" +
                        " from " + tableName + " a " +
                        " left join ordertransactionfront b" +
                        " on a.TransactionID=b.TransactionID" +
                        " and a.ComputerID=b.ComputerID" +
                        " and a.ShopID=b.ShopID" +
                        " where SyncStatus=0 limit 10";

                    var dt = new DataTable();
                    using (var reader = await _db.ExecuteReaderAsync(cmd))
                    {
                        dt.Load(reader);
                    }

                    foreach (DataRow reader in dt.Rows)
                    {
                        var taskId = reader.GetValue<string>("TaskID");
                        var saleDate = reader.GetValue<DateTime>("SaleDate").ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        try
                        {
                            var payload = new
                            {
                                DocNo = reader.GetValue<string>("ReceiptNumber"),
                                DocDate = saleDate,
                                Amount = reader.GetValue<decimal>("ReceiptRetailPrice"),
                                VatAmount = reader.GetValue<decimal>("TransactionVAT"),
                                NetAmount = reader.GetValue<decimal>("ReceiptPayPrice"),
                                Sticker = reader.GetValue<string>("TransactionNote")
                            };

                            await LogManager.Instance.WriteLogAsync($"Send receipt data to commission api {JsonConvert.SerializeObject(payload)}", LogPrefix);
                            cmd.CommandText = "update " + tableName + " set SaleDate=@saleDate, SyncStartTime=@startTime where TaskID=@taskId";
                            cmd.Parameters.Clear();
                            cmd.Parameters.Add(_db.CreateParameter("@saleDate", saleDate));
                            cmd.Parameters.Add(_db.CreateParameter("@startTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                            cmd.Parameters.Add(_db.CreateParameter("@taskId", taskId));
                            await _db.ExecuteNonQueryAsync(cmd);

                            var resp = await HttpClientManager.Instance.PostAsync<object>(commissionUrl, payload);
                            if (resp != null)
                            {
                                cmd.CommandText = "update " + tableName + " set SyncStatus=1, SyncEndTime=@endTime, Message='' where TaskID=@taskId";
                                cmd.Parameters.Clear();
                                cmd.Parameters.Add(_db.CreateParameter("@endTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                                cmd.Parameters.Add(_db.CreateParameter("@taskId", taskId));
                                await _db.ExecuteNonQueryAsync(cmd);

                                result.Message = $"Success send receipt data to commission api";
                                await LogManager.Instance.WriteLogAsync(result.Message, LogPrefix);
                            }
                        }
                        catch (Exception ex)
                        {
                            var errMsg = ex.Message;
                            if (ex is HttpRequestException)
                            {
                                var reqEx = (ex as HttpRequestException);
                                errMsg = $"{reqEx.InnerException.Message} {commissionUrl}";
                            }
                            else if (ex is HttpResponseException)
                            {
                                var respEx = (ex as HttpResponseException);
                                errMsg = $"{(ex as HttpResponseException).Response.ReasonPhrase}";
                            }

                            cmd.CommandText = "update " + tableName + " set Message=@msg where TaskID=@taskId";
                            cmd.Parameters.Clear();
                            cmd.Parameters.Add(_db.CreateParameter("@msg", errMsg));
                            cmd.Parameters.Add(_db.CreateParameter("@taskId", taskId));
                            await _db.ExecuteNonQueryAsync(cmd);

                            await LogManager.Instance.WriteLogAsync($"Fail send receipt data to commission api {result.Message}", LogPrefix, LogManager.LogTypes.Error);

                            result.StatusCode = HttpStatusCode.InternalServerError;
                            result.Message = errMsg;
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
    }
}
