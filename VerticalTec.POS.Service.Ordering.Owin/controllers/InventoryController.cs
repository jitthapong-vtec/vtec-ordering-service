using DevExpress.XtraExport.Helpers;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Services;
using vtecPOS.GlobalFunctions;
using vtecPOS.POSControl;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    [RoutePrefix("v1/inventory")]

    public class InventoryController : ApiController
    {
        private IDatabase _database;
        private VtecPOSRepo _vtecRepo;
        private InventModule _inventModule;

        public InventoryController(IDatabase database, VtecPOSRepo vtecRepo)
        {
            _database = database;
            _vtecRepo = vtecRepo;

            _inventModule = new InventModule();
        }

        [HttpGet]
        [Route("Materials")]
        public async Task<IHttpActionResult> GetMaterials(int documentType = 0)
        {
            using (var conn = (MySqlConnection)await _database.ConnectAsync())
            {
                var docHeader = new InventObject.DocHeader
                {
                    ShopID = 0,
                    DocumentDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DueDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    DocumentTypeID = documentType,
                    DocumentNo = "-",
                    ItemDiscount = "0.00",
                    BillDiscount = "0.00",
                    BillDiscPercent = "0",
                    SubTotalAmt = "0.00",
                    TotalAmt = "0.00",
                    GrandTotalAmt = "0.00",
                    TotalVAT = "0.00",
                    TotalDiscount = "0.00"
                };

                var materials = await Task.Run(() =>
                {
                    var respText = "";
                    var materialData = new InventObject.MaterialData();
                    if (_inventModule.Material_Data(ref respText, ref materialData, docHeader, "", "", conn) == false)
                        throw new Exception(respText);
                    return materialData;
                });

                return Ok(new
                {
                    Status = HttpStatusCode.OK,
                    Data = materials
                });
            }
        }
    }
}
