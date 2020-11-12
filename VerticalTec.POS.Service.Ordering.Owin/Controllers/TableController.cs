using Hangfire;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using VerticalTec.POS.Service.Ordering.Owin.Services;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    public class TableController : ApiController
    {
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
        public async Task<object> GetTablePincodeAsync(string tranKey, int shopId, int tableId)
        {
            var pinData = new
            {
                responseCode = "",
                responseText = "",
                responseObj = new
                {
                    mobileNumber = "",
                    smsHeader = "",
                    smsNumber = "",
                    validateType = 0
                }
            };

            using (var conn = await _database.ConnectAsync())
            {
                var saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, false, true);

                var cmd = _database.CreateCommand(
                    "select ShopKey from shop_data where Deleted=0;" +
                    "select * from weborder_token where SaleDate=@saleDate;", conn);

                cmd.Parameters.Add(_database.CreateParameter("@saleDate", saleDate));

                var ds = new DataSet();
                var adapter = _database.CreateDataAdapter(cmd);
                adapter.TableMappings.Add("Table", "ShopData");
                adapter.TableMappings.Add("Table1", "WebOrderToken");
                adapter.Fill(ds);

                var reqId = ds.Tables["WebOrderToken"].AsEnumerable().FirstOrDefault()?.GetValue<string>("MerchantReqId");
                if (string.IsNullOrEmpty(reqId))
                    reqId = Guid.NewGuid().ToString();


            }
            return pinData;
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
        public async Task<IHttpActionResult> OpenTableAsync(OrderTransaction tranData)
        {
            var result = new HttpActionResult<OrderTransaction>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    await _orderingService.OpenTransactionAsync(conn, tranData);
                    await _orderingService.OpenTransactionProcessAsync(conn, tranData);
                    await _orderingService.UpdateTableStatusAsync(conn, tranData.TransactionID, tranData.ComputerID, tranData.ShopID);

                    await _posRepo.SetComputerAccessAsync(conn, tranData.TableID, tranData.TerminalID);
                    _messenger.SendMessage();

                    _log.Info($"OPEN_TABLE {JsonConvert.SerializeObject(tranData)}");
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = tranData;

                }
                catch (VtecPOSException ex)
                {
                    result.StatusCode = HttpStatusCode.InternalServerError;
                    result.Message = ex.Message;
                }
            }
            return result;
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
        public async Task<IHttpActionResult> CheckAccessAsync(int tableId)
        {
            var result = new HttpActionResult<DataTable>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var dataTable = new DataTable();
                string sqlQuery = "select a.*, b.* from tableno a" +
                    " inner join ComputerName b" +
                    " on a.CurrentAccessComputer=b.ComputerID" +
                    " where a.TableID=@tableId";
                var cmd = _database.CreateCommand(sqlQuery, conn);
                cmd.Parameters.Add(_database.CreateParameter("@tableId", tableId));
                using (IDataReader reader = await _database.ExecuteReaderAsync(cmd))
                {
                    dataTable.Load(reader);
                }
                result.StatusCode = HttpStatusCode.OK;
                result.Body = dataTable;
            }
            return result;
        }

        [HttpPost]
        [Route("v1/tables/setaccess")]
        public async Task<IHttpActionResult> SetComputerAccessAsync(int tableId, int terminalId)
        {
            var result = new HttpActionResult<string>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                await _posRepo.SetComputerAccessAsync(conn, tableId, terminalId);
                result.StatusCode = HttpStatusCode.OK;
                result.Body = "";
            }
            return result;
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
