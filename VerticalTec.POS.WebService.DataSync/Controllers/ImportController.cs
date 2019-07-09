using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.WebService.DataSync.Models;
using vtecPOS_SQL.POSControl;

namespace VerticalTec.POS.WebService.DataSync.Controllers
{
    public class ImportController : ApiController
    {
        IDatabase _database;
        POSModule _posModule;

        public ImportController(IDatabase database, POSModule posModule)
        {
            _database = database;
            _posModule = posModule;
        }

        [HttpPost]
        [Route("v1/import/inv")]
        public async Task<IHttpActionResult> ImportInventoryDataAsync([FromBody]object data)
        {
            var result = new HttpActionResult<string>(Request);
            using (var conn = await _database.ConnectAsync())
            {
                try
                {
                    var respText = "";
                    var syncJson = "";
                    var dataSet = new DataSet();
                    var success = _posModule.ImportInventData(ref syncJson, ref respText, dataSet, data.ToString(), conn as SqlConnection);
                    if (success)
                    {
                        result.StatusCode = HttpStatusCode.Created;
                        result.Data = syncJson;
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.InternalServerError;
                        result.Message = respText;
                    }
                }
                catch (Exception ex)
                {
                    result.Message = ex.Message;
                }
            }
            return result;
        }
    }
}
