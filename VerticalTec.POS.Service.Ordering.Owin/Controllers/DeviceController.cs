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
                        string imageBaseUrl = await _posRepo.GetKioskAdsImageBaseUrlAsync(conn, shopId);

                        var payTypeList = "";
                        cmd.CommandText = "select PayTypeList from computername where DeviceCode=@deviceCode";
                        using (var reader = await _database.ExecuteReaderAsync(cmd))
                        {
                            if (reader.Read())
                                payTypeList = (string)reader["PayTypeList"];
                        }
                        cmd.CommandText =
                            " select * from programpropertyvalue;" +
                            " select Id, concat('" + imageBaseUrl + "', ImageName) as ImageName, ChangeDuration from advertisement;" +
                            " select * from paytype where PayTypeID in (" + payTypeList + ");" +
                            " select * from salemode where deleted=0;" +
                            " select a.*, c.*, d.*, case when b.ProductVATPercent is null then 7.00 else b.ProductVATPercent end as VATPercent from shop_data a " +
                            " left join (select * from productvat where Deleted=0) b on a.VATCode=b.ProductVATCode " +
                            " join brand_data c on a.BrandID=c.BrandID" +
                            " join merchant_data d on a.MerchantID=c.MerchantID where a.ShopID=@shopId;";
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

                        try
                        {
                            var saleDate = await _posRepo.GetSaleDateAsync(conn, shopId, false);
                            dtTerminal.Rows[0]["IsOpenDay"] = 1;
                        }
                        catch { }

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
