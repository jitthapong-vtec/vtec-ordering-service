using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Services;
using vtecPOS.GlobalFunctions;
using vtecPOS.POSControl;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    [BasicAuthenActionFilter]
    [RoutePrefix("v1/kds")]

    public class KDSController : ApiController
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetLogger("logordering");

        private IDatabase _database;
        private VtecPOSRepo _vtecRepo;
        private IPrintService _printService;
        private IMessengerService _messgerService;

        public KDSController(IDatabase database, IPrintService printService, IMessengerService messengerService, VtecPOSRepo vtecRepo)
        {
            _database = database;
            _printService = printService;
            _messgerService = messengerService;
            _vtecRepo = vtecRepo;
        }

        [HttpGet]
        [Route("Computer")]
        public async Task<IHttpActionResult> GetKDSComputerAsync()
        {
            using (var conn = (MySqlConnection)await _database.ConnectAsync())
            {
                var cmd = _database.CreateCommand("select * from ComputerName where ComputerType in (3,4) and Deleted=0;", conn);
                var dt = new DataTable();
                using (var reader = await _database.ExecuteReaderAsync(cmd))
                {
                    dt.Load(reader);
                }
                return Ok(new
                {
                    Status = HttpStatusCode.OK,
                    Data = dt
                });
            }
        }

        [HttpGet]
        [Route("KitchenData")]
        public async Task<IHttpActionResult> GetKDSDataAsync(int kdsId, int shopId)
        {
            using (var conn = (MySqlConnection)await _database.ConnectAsync())
            {
                try
                {
                    var ds = await GetKDSDataAsync(kdsId, shopId, conn);
                    return Ok(new
                    {
                        Status = HttpStatusCode.OK,
                        Data = ds
                    });
                }
                catch (Exception ex)
                {
                    return Ok(new
                    {
                        Status = HttpStatusCode.InternalServerError,
                        Message = ex.Message
                    });
                }
            }
        }

        [HttpPost]
        [Route("Checkout")]
        public async Task<IHttpActionResult> KDSCheckoutAsync(dynamic[] kdsOrders, int kdsId, int shopId, int staffId = 2)
        {
            using (var conn = (MySqlConnection)await _database.ConnectAsync())
            {
                using (_ = new InvariantCultureScope())
                {
                    var saleDate = await _vtecRepo.GetSaleDateAsync(conn, shopId, true, true);
                    var posModule = new POSModule();

                    foreach (var order in kdsOrders)
                    {
                        var respText = "";
                        var ds = new DataSet();

                        var tid = (int)order.TransactionID;
                        var cid = (int)order.ComputerID;
                        var oid = (int)order.OrderDetailID;

                        var success = posModule.KDS_Click(ref respText, ref ds, tid, cid, oid, kdsId, shopId, saleDate, "front", staffId, conn);
                        if (success)
                        {
                            await _printService.PrintAsync(shopId, cid, ds);
                        }
                        else
                        {
                            _logger.Error($"KDS_Click => {respText}");
                        }
                    }

                    var kdsData = await GetKDSDataAsync(kdsId, shopId, conn);
                    return Ok(new
                    {
                        Status = HttpStatusCode.OK,
                        Data = kdsData
                    });
                }
            }
        }

        private async Task<DataSet> GetKDSDataAsync(int kdsId, int shopId, MySqlConnection conn)
        {
            using (_ = new InvariantCultureScope())
            {
                var saleDate = await _vtecRepo.GetSaleDateAsync(conn, shopId, true, true);
                var posModule = new POSModule();
                var respText = "";
                var ds = new DataSet();
                var success = posModule.KDS_Data(ref respText, ref ds, kdsId, 0, 0, shopId, saleDate, "front", conn);
                if (!success)
                    throw new Exception(respText);
                return ds;
            }
        }
    }
}
