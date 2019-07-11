using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin.Models;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync.Owin.Controllers
{
    public class SaleController : ApiController
    {
        IDatabase _database;
        POSModule _posModule;

        public SaleController(IDatabase database, POSModule posModule)
        {
            _database = database;
            _posModule = posModule;
        }

        [HttpGet]
        [Route("v1/sale/sendtohq")]
        public async Task<IHttpActionResult> SendSaleAsync()
        {
            var result = new HttpActionResult<string>(Request);

            return result;
        }
    }
}
