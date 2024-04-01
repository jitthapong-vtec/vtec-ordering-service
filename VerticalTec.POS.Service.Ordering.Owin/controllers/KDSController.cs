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
            _vtecRepo = vtecRepo;
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
                    var success = posModule.KDS_Data(ref respText, ref ds, kdsId, 0, 0, shopId, saleDate.ToString("yyyy-MM-dd"), "front", conn);
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
