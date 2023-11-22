using Hangfire;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Service.Ordering.Owin.Services;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    [BasicAuthenActionFilter]
    public class TableController : ApiController
    {
        public static object lockObj = new object();

        static readonly NLog.Logger _log = NLog.LogManager.GetLogger("logtable");

        IDatabase _database;
        IOrderingService _orderingService;
        IMessengerService _messenger;
        IPrintService _printService;

        VtecPOSRepo _posRepo;

        public TableController(IDatabase database, IOrderingService orderingService, IMessengerService messenger, IPrintService printService)
        {
            _database = database;
            _orderingService = orderingService;
            _messenger = messenger;
            _printService = printService;
            _posRepo = new VtecPOSRepo(database);
        }

        [HttpGet]
        [Route("v1/tables/pincode")]
        public async Task<IHttpActionResult> GetTablePincodeAsync(string tranKey, int shopId, int tableId, string saleDate = "")
        {
            var pinCode = "";
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    pinCode = await _orderingService.GetOrRegenPincodeAsync(conn, tranKey, shopId, tableId, saleDate: saleDate);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }
            }
            return Ok(pinCode);
        }

        [HttpGet]
        [Route("v1/tables/buffetoption")]
        public async Task<IHttpActionResult> GetBuffetOptionAsync(int tranId, int compId, int shopId)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = (await _database.ConnectAsync() as MySqlConnection))
            {
                var posModule = new POSModule();
                var dtBuffetOption = new DataTable();
                int buffetOptionId = 0;
                var qddid = 0;
                var respText = "";
                var saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, false, true);
                var success = posModule.Buffet_TypeOption(ref respText, ref dtBuffetOption, ref buffetOptionId, ref qddid, tranId, compId, shopId, saleDate, "front", conn);
                if (!success)
                {
                    result.StatusCode = HttpStatusCode.BadRequest;
                    result.Message = respText;
                    return result;
                }

                var bfOptions = dtBuffetOption.AsEnumerable().Select(dr => new
                {
                    QuestionID = dr.GetValue<int>("QDDID"),
                    OptionID = dr.GetValue<int>("OptionID"),
                    OptionName = dr.GetValue<string>("OptionName"),
                    Selected = dr.GetValue<int>("OptionID") == buffetOptionId
                });
                result.StatusCode = HttpStatusCode.OK;
                result.Body = bfOptions;
                return result;
            }
        }

        [HttpPost]
        [Route("v1/tables/buffetoption")]
        public async Task<IHttpActionResult> ChangeBuffetTypeAsync(int tableId, int tranId, int compId, int qddid, int optionId, int shopId, int staffId, int langId = 1)
        {
            var result = new HttpActionResult<object>(Request);
            try
            {
                using (var conn = await (_database.ConnectAsync()) as MySqlConnection)
                {
                    var posModule = new POSModule();
                    var respText = "";
                    var saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, false, true);
                    var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);
                    var success = posModule.Buffet_ChangeType(ref respText, tranId, compId, qddid, optionId, shopId, saleDate, "front", decimalDigit, staffId, langId, conn);
                    if (!success)
                    {
                        result.StatusCode = HttpStatusCode.BadRequest;
                        result.Message = respText;
                        return result;
                    }
                    var receiptString = "";
                    var receiptCopyString = "";
                    var noCopy = 0;
                    var resultData = new DataSet();

                    var updateResult = await _orderingService.UpdateBuffetAsync(conn, $"{tranId}:{compId}", shopId, tableId);
                    result.StatusCode = HttpStatusCode.OK;
                    result.Message = "Success";

                    posModule.BillDetail(ref respText, ref receiptString, ref receiptCopyString, ref noCopy, ref resultData,
                        (int)ViewBillTypes.Default, tranId, compId, shopId, 0, "front", langId, conn);

                    _log.Info($"Update buffet type: {updateResult}");
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = ex.Message;
            }
            return result;

        }

        [HttpPost]
        [Route("v1/tables/move")]
        public async Task<IHttpActionResult> MoveTableAsync(TableManage table)
        {
            var result = new HttpActionResult<TableManage>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var dsPrintData = await _orderingService.MoveTableOrderAsync(conn, table.TransactionID, table.ComputerID,
                        table.ShopID, table.StaffID, table.LangID, table.ToTableIds, table.ReasonList, table.ReasonText);

                    await _printService.PrintAsync(table.ShopID, table.ComputerID, dsPrintData);
                    _messenger.SendMessage();

                    try
                    {
                        var param = await _posRepo.GetPropertyValueAsync(conn, 1130, "TableRequestPinCode");
                        if (param == "1")
                            await _orderingService.GetOrRegenPincodeAsync(conn, $"{table.TransactionID}:{table.ComputerID}", table.ShopID, table.ToTableID, 2);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Regen pin code {ex.Message}");
                    }

                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = table;

                    _log.Info($"MOVE_TABLE {JsonConvert.SerializeObject(table)}");
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;

                    _log.Info($"MOVE_TABLE {ex.Message}");
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/tables/merge")]
        public async Task<IHttpActionResult> MergeTableAsync(TableManage table)
        {
            var result = new HttpActionResult<TableManage>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var dsPrintData = await _orderingService.MergeTableOrderAsync(conn, table.TransactionID, table.ComputerID,
                        table.ShopID, table.StaffID, table.LangID, table.ToTableIds, table.ReasonList, table.ReasonText);

                    await _printService.PrintAsync(table.ShopID, table.ComputerID, dsPrintData);
                    _messenger.SendMessage();

                    try
                    {
                        var param = await _posRepo.GetPropertyValueAsync(conn, 1130, "TableRequestPinCode");
                        if (param == "1")
                            await _orderingService.GetOrRegenPincodeAsync(conn, $"{table.TransactionID}:{table.ComputerID}", table.ShopID, table.FromTableID, 2);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Regen pin code {ex.Message}");
                    }

                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = table;

                    _log.Info($"MERGE_TABLE {JsonConvert.SerializeObject(table)}");
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;

                    _log.Info($"MOVE_TABLE {ex.Message}");
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/tables/split")]
        public async Task<IHttpActionResult> SplitTableAsync(TableManage table)
        {
            var result = new HttpActionResult<TableManage>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var dsPrintData = await _orderingService.SplitTableOrderAsync(conn, table.TransactionID, table.ComputerID,
                        table.ShopID, table.StaffID, table.LangID, table.ToTableIds, table.ReasonList, table.ReasonText);

                    await _printService.PrintAsync(table.ShopID, table.ComputerID, dsPrintData);
                    _messenger.SendMessage();

                    try
                    {
                        var param = await _posRepo.GetPropertyValueAsync(conn, 1130, "TableRequestPinCode");
                        if (param == "1")
                            await _orderingService.GetOrRegenPincodeAsync(conn, $"{table.TransactionID}:{table.ComputerID}", table.ShopID, table.FromTableID, 2);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Regen pin code {ex.Message}");
                    }

                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = table;

                    _log.Info($"SPLIT_TABLE {JsonConvert.SerializeObject(table)}");
                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;

                    _log.Info($"MOVE_TABLE {ex.Message}");
                }
            }
            return result;
        }

        [HttpPost]
        [Route("v1/tables/open")]
        public IHttpActionResult OpenTableAsync(OrderTransaction tranData)
        {
            lock (lockObj)
            {
                _log.Info($"OPEN_TABLE {JsonConvert.SerializeObject(tranData)}");

                var result = new HttpActionResult<OrderTransaction>(Request);
                using (var conn = _database.Connect())
                {
                    try
                    {
                        var openTableTask = _orderingService.OpenTransactionAsync(conn, tranData);
                        var openTranTask = _orderingService.OpenTransactionProcessAsync(conn, tranData);
                        var updateTableStatusTask = _orderingService.UpdateTableStatusAsync(conn, tranData.TransactionID, tranData.ComputerID, tranData.ShopID);

                        var setComputerAccessTask = _posRepo.SetComputerAccessAsync(conn, tranData.TableID, tranData.TerminalID);
                        _messenger.SendMessage();

                        _log.Info($"TABLE_DATA {JsonConvert.SerializeObject(tranData)}");

                        var tasks = new Task[]
                        {
                            openTableTask,
                            openTranTask,
                            updateTableStatusTask,
                            setComputerAccessTask
                        };
                        try
                        {
                            Task.WaitAll(tasks);
                        }
                        catch (Exception ex)
                        {
                            throw new VtecPOSException(ex.InnerException?.Message ?? ex.Message, ex.InnerException);
                        }

                        result.StatusCode = HttpStatusCode.OK;
                        result.Body = tranData;
                    }
                    catch (VtecPOSException ex)
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = ex.Message;
                        _log.Error(ex, $"TABLE_DATA {ex.Message}");
                    }
                }
                return result;
            }
        }

        [HttpPost]
        [Route("v1/tables/close")]
        public async Task<IHttpActionResult> CloseTableAsync(OrderTransaction tranData)
        {
            var result = new HttpActionResult<OrderTransaction>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var posModule = new POSModule();
                var responseText = "";
                var saleDate = await _posRepo.GetSaleDateAsync(conn, tranData.ShopID, true);
                var decimalDigit = await _posRepo.GetDefaultDecimalDigitAsync(conn);

                var isSuccess = posModule.Table_Close(ref responseText, "front", tranData.TransactionID, tranData.ComputerID,
                    tranData.ShopID, saleDate, decimalDigit, conn as MySqlConnection);
                await _posRepo.SetComputerAccessAsync(conn, tranData.TableID, 0);

                if (isSuccess)
                {
                    _messenger.SendMessage();
                }
                else
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = responseText;
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/tables/queuemode")]
        public async Task<IHttpActionResult> CheckInputQueueMode(SaleModes saleMode, int shopId, int terminalId)
        {
            var result = new HttpActionResult<bool>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var responseText = "";
                var posModule = new POSModule();
                var inputQueueMode = false;

                posModule.InputTableBySaleMode(ref responseText, ref inputQueueMode, (int)saleMode, shopId, terminalId, conn as MySqlConnection);

                result.StatusCode = HttpStatusCode.OK;
                result.Body = inputQueueMode;
            }
            return result;
        }

        [HttpGet]
        [Route("v1/tables/getaccess")]
        public IHttpActionResult CheckAccessAsync(int tableId)
        {
            lock (lockObj)
            {
                var result = new HttpActionResult<DataTable>(Request);
                using (var conn = _database.Connect())
                {
                    var dataTable = new DataTable();
                    string sqlQuery = "select a.*, b.* from tableno a" +
                        " inner join ComputerName b" +
                        " on a.CurrentAccessComputer=b.ComputerID" +
                        " where a.TableID=@tableId";
                    var cmd = _database.CreateCommand(sqlQuery, conn);
                    cmd.Parameters.Add(_database.CreateParameter("@tableId", tableId));
                    using (IDataReader reader = _database.ExecuteReaderAsync(cmd).Result)
                    {
                        dataTable.Load(reader);
                    }
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = dataTable;
                }
                return result;
            }
        }

        [HttpPost]
        [Route("v1/tables/setaccess")]
        public IHttpActionResult SetComputerAccessAsync(int tableId, int terminalId)
        {
            lock (lockObj)
            {
                var result = new HttpActionResult<string>(Request);
                using (var conn = _database.Connect())
                {
                    _posRepo.SetComputerAccessAsync(conn, tableId, terminalId).Wait();
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = "";
                }
                return result;
            }
        }

        [HttpPost]
        [Route("v1/tables/ordertran/update")]
        public async Task<IHttpActionResult> UpdateTableAsync(OrderTransaction data)
        {
            var result = new HttpActionResult<OrderTransaction>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                if (data.TransactionID > 0 && data.ComputerID > 0)
                {
                    await _posRepo.UpdateCustomerQtyAsync(conn, data.TransactionID, data.ComputerID, data.TotalCustomer);
                    await _posRepo.UpdateTransactionNameAsync(conn, data.TransactionID, data.ComputerID, data.TransactionName);
                    await _posRepo.AddQuestionAsync(conn, data);

                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = data;
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/tables")]
        public async Task<IHttpActionResult> GetTableDataAsync(int shopId, int terminalId)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var zoneIds = "";
                var dtComputer = await _posRepo.GetComputerAsync(conn, terminalId);
                if (dtComputer.Rows.Count > 0)
                {
                    zoneIds = dtComputer.Rows[0].GetValue<string>("tablezonelist");
                }
                var dtTableZone = await _posRepo.GetTableZoneAsync(conn, shopId, zoneIds);
                var dtTable = await _posRepo.GetTableAsync(conn, shopId, "");

                var tableList = (from zone in dtTableZone.AsEnumerable()
                                 select new
                                 {
                                     ZoneID = zone.GetValue<int>("ZoneID"),
                                     ZoneName = zone.GetValue<string>("ZoneName"),
                                     Tables = (from table in dtTable.AsEnumerable()
                                               where table.GetValue<int>("ZoneID") == zone.GetValue<int>("ZoneID")
                                               select new
                                               {
                                                   TransactionID = table.GetValue<int>("TransactionID"),
                                                   ComputerID = table.GetValue<int>("ComputerID"),
                                                   TableID = table.GetValue<int>("TableID"),
                                                   ZoneID = zone.GetValue<int>("ZoneID"),
                                                   TableNumber = table.GetValue<int>("TableNumber"),
                                                   TableName = table.GetValue<string>("TableName"),
                                                   Capacity = table.GetValue<int>("Capacity"),
                                                   Status = table.GetValue<int>("Status"),
                                                   IsWarning = table.GetValue<bool>("IsWarning"),
                                                   IsCritical = table.GetValue<bool>("IsCritical"),
                                                   BuffetColorHex = table.GetValue<string>("BuffetColorHex"),
                                                   TableTimeMinute = table.GetValue<int>("TableTimeMinute"),
                                                   CurrentAccessComputer = table.GetValue<int>("CurrentAccessComputer"),
                                                   CustomerName = table.GetValue<string>("TransactionName"),
                                                   TotalCustomer = table.GetValue<int>("NoCustomer"),
                                                   GroupNo = table.GetValue<int>("GroupNo")
                                               }).ToList()
                                 }).ToList();

                result.StatusCode = HttpStatusCode.OK;
                result.Body = tableList;
            }
            return result;
        }

        [HttpGet]
        [Route("v1/tables/questions")]
        public async Task<IHttpActionResult> GetQuestionAsync(int shopId, int transactionId, int terminalId)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var ds = await _posRepo.GetQuestionAsync(conn, shopId, transactionId, terminalId);
                var dtQuestion = ds.Tables["Question"];
                var dtQuestionTransaction = ds.Tables["QuestionTransaction"];

                var question = (from row in dtQuestion.AsEnumerable()
                                group row by row["QDDGID"] into gj
                                select new
                                {
                                    QuestionGroupID = gj.FirstOrDefault().GetValue<int>("QDDGID"),
                                    QuestionGroupName = gj.FirstOrDefault().GetValue<string>("QDDGName"),
                                    Questions = (from q in dtQuestion.AsEnumerable()
                                                 where q.GetValue<int>("QDDGID") == gj.FirstOrDefault().GetValue<int>("QDDGID")
                                                 group q by q["QDDID"] into qg
                                                 let questionId = qg.FirstOrDefault().GetValue<int>("QDDID")
                                                 select new
                                                 {
                                                     QuestionID = questionId,
                                                     QuestionName = qg.FirstOrDefault().GetValue<string>("QDDName"),
                                                     QuestionType = qg.FirstOrDefault().GetValue<int>("QDDTypeID"),
                                                     QuestionValue = (from s in dtQuestionTransaction.AsEnumerable()
                                                                      where s.GetValue<int>("QDDID") == questionId
                                                                      select s).FirstOrDefault()?.GetValue<double>("QDVValue") ?? 0,
                                                     IsRequired = qg.FirstOrDefault().GetValue<int>("IsRequired"),
                                                     Options = (from o in qg.ToList()
                                                                where o.GetValue<int>("QDDID") == qg.FirstOrDefault().GetValue<int>("QDDID")
                                                                select new
                                                                {
                                                                    QuestionID = o.GetValue<int>("QDDID"),
                                                                    OptionID = o.GetValue<int>("OptionID"),
                                                                    OptionName = o.GetValue<string>("OptionName"),
                                                                    Selected = (from s in dtQuestionTransaction.AsEnumerable()
                                                                                where s.GetValue<int>("QDDID") == o.GetValue<int>("QDDID") && s.GetValue<int>("OptionID") == o.GetValue<int>("OptionID")
                                                                                select s).FirstOrDefault()?.GetValue<int>("OptionID") > 0 ? true : false
                                                                }).ToList()
                                                 })

                                }).ToList();
                result.StatusCode = HttpStatusCode.OK;
                result.Body = question;
            }
            return result;
        }
        //TODO: v1/tables/{tableId:int}/transaction to v1/tables/ordertran
        [HttpGet]
        [Route("v1/tables/ordertran")]
        public async Task<IHttpActionResult> GetTableTranData(int tableId)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var cmd = _database.CreateCommand("select a.*, b.* from tableno a" +
                    " left join order_tablefront b" +
                    " on a.TableID=b.TableID " +
                    " where a.TableID=@tableId", conn);
                cmd.Parameters.Add(_database.CreateParameter("@tableId", tableId));
                IDataAdapter adapter = _database.CreateDataAdapter(cmd);
                adapter.TableMappings.Add("Table", "TableName");

                var dataSet = new DataSet("TableName");
                adapter.Fill(dataSet);
                var tableTran = (from row in dataSet.Tables[0].AsEnumerable().AsParallel()
                                 select new
                                 {
                                     TransactionID = row.GetValue<int>("TransactionID"),
                                     ComputerID = row.GetValue<int>("ComputerID"),
                                     TableName = row.GetValue<string>("TableName")
                                 }).FirstOrDefault();
                if (tableTran != null && tableTran.TransactionID > 0)
                {
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = tableTran;
                }
                else
                {
                    result.StatusCode = HttpStatusCode.NotFound;
                    result.Message = $"Not found transaction {tableId}";
                }
            }
            return result;
        }

        [HttpGet]
        [Route("v1/tables/{shopId:int}/{tableId:int}")]
        public async Task<IHttpActionResult> GetTableStatusAsync(int shopId, int tableId)
        {
            var result = new HttpActionResult<object>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var cmd = _database.CreateCommand("select a.* from tableno a join tablezone b " +
                    "on a.ZoneID=b.ZoneID where a.Deleted=0 and a.TableID=@tableId and b.ShopID=@shopId", conn);
                cmd.Parameters.Add(_database.CreateParameter("@tableId", tableId));
                cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        result.StatusCode = HttpStatusCode.OK;
                        result.Body = new
                        {
                            TableID = reader.GetValue<int>("TableID"),
                            ZoneID = reader.GetValue<int>("ZoneID"),
                            TableNumber = reader.GetValue<string>("TableNumber"),
                            TableName = reader.GetValue<string>("TableName"),
                            Status = reader.GetValue<int>("Status")
                        };
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.NoContent;
                    }
                }
            }
            return result;
        }
    }
}
