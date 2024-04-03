using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using vtecPOS.GlobalFunctions;
using vtecPOS.POSControl;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    [BasicAuthenActionFilter]
    [RoutePrefix("v1/kds")]

    public class KDSController : ApiController
    {
        private IDatabase _database;
        private VtecPOSRepo _vtecRepo;

        public KDSController(IDatabase database, VtecPOSRepo vtecRepo)
        {
            _database = database;
            _vtecRepo = vtecRepo;
        }

        [HttpGet]
        [Route("KDS_Computer")]
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
        [Route("KDS_Data")]
        public async Task<IHttpActionResult> GetKDSDataAsync(int kdsId, int shopId, DateTime saleDate)
        {
            using (var conn = (MySqlConnection)await _database.ConnectAsync())
            {
                using (_ = new InvariantCultureScope())
                {
                    var posModule = new POSModule();
                    var respText = "";
                    var ds = new DataSet();
                    var saleDateStr = "{ d '" + saleDate.ToString("yyyy-MM-dd") + "' }";
                    var success = posModule.KDS_Data(ref respText, ref ds, kdsId, 0, 0, shopId, saleDateStr, "front", conn);
                    if (success)
                    {
                        return Ok(new
                        {
                            Status = HttpStatusCode.OK,
                            Data = ds
                        });
                    }
                    else
                    {
                        return Ok(new
                        {
                            Status = HttpStatusCode.InternalServerError,
                            Message = respText
                        });
                    }
                }
            }
        }

        [HttpGet]
        [Route("KDS_Summary_Click")]
        public async Task<IHttpActionResult> KDSSummaryClickAsync(int transactionId, int computerId, int orderDetailId, int kdsId, int shopId, DateTime saleDate, int staffId)
        {
            using (var conn = (MySqlConnection)await _database.ConnectAsync())
            {
                using (_ = new InvariantCultureScope())
                {
                    var posModule = new POSModule();
                    var respText = "";
                    var ds = new DataSet();
                    var saleDateStr = "{ d '" + saleDate.ToString("yyyy-MM-dd") + "' }";
                    var success = posModule.KDS_SummaryClick(ref respText, ref ds, transactionId, computerId, orderDetailId, kdsId, shopId, saleDateStr, "front", staffId, conn);
                    if (success)
                    {
                        return Ok(new
                        {
                            Status = HttpStatusCode.OK,
                            Data = ds
                        });
                    }
                    else
                    {
                        return Ok(new
                        {
                            Status = HttpStatusCode.InternalServerError,
                            Message = respText
                        });
                    }
                }
            }
        }
    }
}
