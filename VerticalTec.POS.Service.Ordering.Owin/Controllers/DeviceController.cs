using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Utils;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using vtecPOS.GlobalFunctions;
using VerticalTec.POS.Service.Ordering.Owin.Services;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    [RoutePrefix("v1/devices")]
    public class DeviceController : ApiController
    {
        IDatabase _database;
        VtecPOSRepo _posRepo;

        public DeviceController(IDatabase database, IMessengerService messenger)
        {
            _database = database;
            _posRepo = new VtecPOSRepo(database);
        }

        [HttpGet]
        [Route("kiosk")]
        public async Task<IHttpActionResult> VerifyKioskTerminalAsync(string uuid)
        {
            var response = new HttpActionResult<DataSet>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var cmd = _database.CreateCommand("select *, 0 as IsOpenDay from computername where ComputerType=2 and DeviceCode=@deviceCode", conn);
                    cmd.Parameters.Add(_database.CreateParameter("@deviceCode", uuid));
                    var adapter = _database.CreateDataAdapter(cmd);

                    var dataSet = new DataSet();
                    adapter.Fill(dataSet);
                    var dtTerminal = dataSet.Tables[0];
                    dtTerminal.TableName = "Device";

                    if (dtTerminal.Rows.Count > 0)
                    {
                        var shopId = dtTerminal.Rows[0].GetValue<int>("ShopID");
                        string imageBaseUrl = await _posRepo.GetResourceUrl(conn, shopId);

                        cmd.CommandText =
                            " select * from programpropertyvalue;" +
                            " select Id, concat('" + imageBaseUrl + "', ImageName) as ImageName, ChangeDuration from advertisement;" +
                            " select * from paytype where PayTypeID in (select PayTypeList from computername where DeviceCode=@deviceCode);" +
                            " select * from salemode where deleted=0;" +
                            " select a.*, case when b.ProductVATPercent is null then 7.00 else b.ProductVATPercent end as VATPercent from shop_data a left join (select * from productvat where Deleted=0) b on a.VATCode=b.ProductVATCode where a.ShopID=@shopId;";
                        cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));

                        adapter = _database.CreateDataAdapter(cmd);
                        adapter.TableMappings.Add("Table", "Property");
                        adapter.TableMappings.Add("Table1", "Ads");
                        adapter.TableMappings.Add("Table2", "PayType");
                        adapter.TableMappings.Add("Table3", "SaleMode");
                        adapter.TableMappings.Add("Table4", "ShopData");
                        adapter.Fill(dataSet);

                        try
                        {
                            cmd.CommandText = "select * from kiosk_lang where Activated=1";
                            var dtLanguage = new DataTable("Language");
                            using (var reader = await _database.ExecuteReaderAsync(cmd))
                            {
                                dtLanguage.Load(reader);
                            }
                            dataSet.Tables.Add(dtLanguage);
                        }
                        catch (Exception) { }

                        var terminalId = dtTerminal.Rows[0].GetValue<int>("ComputerID");
                        var allowKioskOpenDay = await _posRepo.GetPropertyValueAsync(conn, 2002, "AllowKioskOpenDay", shopId) == "1";

                        var openDayComputer = await _posRepo.GetCashierComputerIdAsync(conn, shopId);

                        if (openDayComputer > 0 || allowKioskOpenDay)
                        {
                            cmd = _database.CreateCommand("select * from staffs where StaffRoleID = 2", conn);
                            var staffId = 2;
                            var staffName = "";
                            using (var reader = await _database.ExecuteReaderAsync(cmd))
                            {
                                if (reader.Read())
                                {
                                    staffId = reader.GetValue<int>("StaffID");
                                    staffName = $"{reader.GetValue<string>("StaffFirstName")} {reader.GetValue<string>("StaffLastName")}";
                                }
                            }

                            if (openDayComputer == 0 && allowKioskOpenDay)
                            {
                                string responseText = "";
                                cmd = _database.CreateCommand("delete from order_tablefront where SaleDate < @saleDate and ShopID=@shopId", conn);
                                cmd.Parameters.Add(_database.CreateParameter("@saleDate", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InstalledUICulture)));
                                cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                                cmd.ExecuteNonQuery();

                                cmd = _database.CreateCommand("update tableno set Status=@status where Status > 0", conn);
                                cmd.Parameters.Add(_database.CreateParameter("@status", 0));
                                await _database.ExecuteNonQueryAsync(cmd);

                                var posModule = new POSModule();
                                posModule.Enday_Auto(ref responseText, shopId, terminalId, staffId, conn as MySqlConnection);
                            }

                            var sessionDate = await _posRepo.GetSaleDateAsync(conn, shopId, false, true);
                            cmd = _database.CreateCommand("select ComputerID from session " +
                                " where SessionDate=@sessionDate " +
                                " and ComputerID=@computerId" +
                                " and ShopID=@shopId", conn);
                            cmd.Parameters.Add(_database.CreateParameter("@sessionDate", sessionDate));
                            cmd.Parameters.Add(_database.CreateParameter("@computerId", terminalId));
                            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));

                            var allowKioskOpenSession = allowKioskOpenDay;
                            if (!allowKioskOpenSession)
                                allowKioskOpenSession = await _posRepo.GetPropertyValueAsync(conn, 2002, "AllowKioskOpenSession", shopId) == "1";

                            if (allowKioskOpenSession)
                            {
                                var isOpenSession = false;
                                using (var reader = await _database.ExecuteReaderAsync(cmd))
                                {
                                    if (reader.Read())
                                    {
                                        if (reader.GetValue<int>("ComputerID") > 0)
                                        {
                                            isOpenSession = true;
                                        }
                                    }
                                }
                                if (!isOpenSession)
                                {
                                    cmd = _database.CreateCommand(
                                        " select max(SessionID) as SessionID from session" +
                                        " where ComputerID=@computerId " +
                                        " and ShopID=@shopId", conn);
                                    cmd.Parameters.Add(_database.CreateParameter("@computerId", terminalId));
                                    cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));

                                    var sessionId = 0;
                                    using (var reader = await _database.ExecuteReaderAsync(cmd))
                                    {
                                        if (reader.Read())
                                        {
                                            sessionId = reader.GetValue<int>("SessionID") + 1;
                                        }
                                    }

                                    cmd = _database.CreateCommand(
                                        " insert into session(SessionID, ComputerID, SessionKey, ComputerName, OpenStaffID, OpenStaff, OpenSessionDateTime, SessionDate, ShopID)" +
                                        " values (@sessionId, @computerId, @sessionKey, @computerName, @openStaffId, @openStaff, @openDateTime, @sessionDate, @shopId)", conn);
                                    cmd.Parameters.Add(_database.CreateParameter("@sessionId", sessionId));
                                    cmd.Parameters.Add(_database.CreateParameter("@computerId", terminalId));
                                    cmd.Parameters.Add(_database.CreateParameter("@sessionKey", $"{sessionId}:{terminalId}"));
                                    cmd.Parameters.Add(_database.CreateParameter("@computerName", dtTerminal.Rows[0].GetValue<string>("ComputerName")));
                                    cmd.Parameters.Add(_database.CreateParameter("@openStaffId", staffId));
                                    cmd.Parameters.Add(_database.CreateParameter("@openStaff", staffName));
                                    cmd.Parameters.Add(_database.CreateParameter("@openDateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
                                    cmd.Parameters.Add(_database.CreateParameter("@sessionDate", sessionDate));
                                    cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                                    await _database.ExecuteNonQueryAsync(cmd);

                                    if (allowKioskOpenDay)
                                    {
                                        cmd = _database.CreateCommand("insert into sessionenddaydetail(SessionDate, ShopID, OpenDayDateTime)" +
                                            " values(@sessionDate, @shopId, @openDayDateTime)", conn);
                                        cmd.Parameters.Add(_database.CreateParameter("@sessionDate", sessionDate));
                                        cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                                        cmd.Parameters.Add(_database.CreateParameter("@openDayDateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
                                        await _database.ExecuteNonQueryAsync(cmd);
                                    }
                                }
                            }
                            dtTerminal.Rows[0]["IsOpenDay"] = 1;
                        }
                        response.StatusCode = HttpStatusCode.OK;
                        response.Body = dataSet;
                    }
                    else
                    {
                        response.StatusCode = HttpStatusCode.NotFound;
                        response.ErrorCode = ErrorCodes.NotFoundRegisteredDevice;
                        response.Message = "This terminal did not register!";
                    }
                }
            }
            catch (Exception ex)
            {
                var errMsg = ex.Message;
                if (ex is MySqlException myEx)
                    errMsg = "Can't connect to database";
                response.StatusCode = HttpStatusCode.InternalServerError;
                response.Message = errMsg;
            }
            return response;
        }

        [HttpGet]
        [Route("mobile")]
        public async Task<IHttpActionResult> VerifyMobileDeviceAsync(string deviceId)
        {
            var result = new HttpActionResult<DataSet>(Request);
            try
            {
                using (var conn = await _database.ConnectAsync())
                {
                    var cmd = _database.CreateCommand(
                        " select * from computername where Deleted=0 and ComputerType=2 and DeviceCode=@deviceCode;" +
                        " select * from programpropertyvalue;" +
                        " select * from printers where deleted=0", conn);
                    cmd.Parameters.Add(_database.CreateParameter("@deviceCode", deviceId));
                    IDataAdapter adapter = _database.CreateDataAdapter(cmd);
                    DataSet dataSet = new DataSet();
                    adapter.TableMappings.Add("Table", "Device");
                    adapter.TableMappings.Add("Table1", "Property");
                    adapter.TableMappings.Add("Table2", "Printer");
                    adapter.Fill(dataSet);
                    DataTable dtDevice = dataSet.Tables["Device"];
                    if (dtDevice.Rows.Count > 0)
                    {
                        var shopId = dtDevice.Rows[0].GetValue<int>("ShopID");

                        cmd = _database.CreateCommand("select * from shop_data where ShopID=@shopId", conn);
                        cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
                        adapter = _database.CreateDataAdapter(cmd);
                        adapter.TableMappings.Add("Table", "ShopData");
                        adapter.Fill(dataSet);

                        cmd = _database.CreateCommand("select * from salemode where deleted=0", conn);
                        adapter = _database.CreateDataAdapter(cmd);
                        adapter.TableMappings.Add("Table", "SaleMode");
                        adapter.Fill(dataSet);

                        result.StatusCode = HttpStatusCode.OK;
                        result.Body = dataSet;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.NotFound;
                        result.ErrorCode = ErrorCodes.NotFoundRegisteredDevice;
                        result.Message = $"Device {deviceId} not registred";
                    }
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = HttpStatusCode.InternalServerError;
                result.Message = $"Get device data error {ex.Message}";
            }
            return result;
        }

        [HttpGet]
        [Route("printers")]
        public async Task<IHttpActionResult> GetPritnersAsync()
        {
            var result = new HttpActionResult<DataTable>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                var cmd = _database.CreateCommand("select * from printers where deleted=0", conn);
                var adapter = _database.CreateDataAdapter(cmd);
                var dataSet = new DataSet();
                adapter.Fill(dataSet);
                adapter.TableMappings.Add("Table", "Printer");

                if (dataSet.Tables.Count > 0)
                {
                    result.StatusCode = HttpStatusCode.OK;
                    result.Body = dataSet.Tables[0];
                }
                else
                {
                    result.StatusCode = HttpStatusCode.NotFound;
                    result.Message = "Not found printers";
                }
            }
            return result;
        }
    }
}
