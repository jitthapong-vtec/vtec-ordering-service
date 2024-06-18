using DevExpress.XtraExport.Helpers;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    [RoutePrefix("inventory")]

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

        [HttpPost]
        [Route("login")]
        public async Task<IHttpActionResult> LoginAsync(object data)
        {
            try
            {
                var jObj = JObject.Parse(data.ToString());
                using (var conn = (MySqlConnection)await _database.ConnectAsync())
                {
                    var cmd = new MySqlCommand("select StaffID, StaffRoleID, StaffFirstName, StaffLastName from staffs where Deleted=0 and StaffLogin=@username and StaffPassword=UPPER(SHA1(@password))", conn);
                    cmd.Parameters.Add(new MySqlParameter("@username", jObj["username"]));
                    cmd.Parameters.Add(new MySqlParameter("@password", jObj["password"]));

                    var dt = new DataTable();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        dt.Load(reader);
                    }

                    if (dt.Rows.Count > 0)
                    {
                        return Ok(new
                        {
                            Status = HttpStatusCode.OK,
                            StatusCode = "200.200",
                            Data = dt.AsEnumerable().Select(r => new
                            {
                                StaffID = r["StaffID"],
                                StaffRoleID = r["StaffRoleID"],
                                StaffFirstName = r["StaffFirstName"],
                                SstaffLastName = r["StaffLastName"]
                            }).FirstOrDefault()
                        });
                    }
                    else
                    {
                        return Ok(new
                        {
                            Status = HttpStatusCode.NotFound,
                            StatusCode = "404.404",
                            Message = "Not found staff information"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = HttpStatusCode.InternalServerError,
                    StatusCode = "500.500",
                    Message = ex.Message
                });
            }
        }

        [HttpGet]
        [Route("shops")]
        public async Task<IHttpActionResult> GetShopAsync()
        {
            try
            {
                using (var conn = (MySqlConnection)await _database.ConnectAsync())
                {
                    var cmd = new MySqlCommand("select ShopID,ShopCode,ShopName from shop_data where Deleted=0 and IsInv=1", conn);
                    var dt = new DataTable();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        dt.Load(reader);
                    }
                    return Ok(new
                    {
                        Status = HttpStatusCode.OK,
                        StatusCode = "200.200",
                        Data = dt
                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = HttpStatusCode.InternalServerError,
                    StatusCode = "500.500",
                    Message = ex.Message
                });
            }
        }

        [HttpPost]
        [Route("hht/createdocument")]
        public async Task<IHttpActionResult> CreateDocument(InventObject.DocumentData documentData, int staffId = 2, int langId = 1)
        {
            try
            {
                using (var conn = (MySqlConnection)await _database.ConnectAsync())
                {
                    var docParam = new InventObject.DocParam()
                    {
                        StaffID = staffId,
                        DocumentDate = documentData.DocHeader.DocumentDate,
                        DocumentTypeID = documentData.DocHeader.DocumentTypeID,
                        LangID = langId
                    };

                    var docData = await Task.Run(() =>
                    {
                        var respText = "";
                        var resultDocData = new InventObject.DocumentData();
                        using (var _ = new InvariantCultureScope())
                        {
                            if (_inventModule.DocumentProcess(ref respText, ref resultDocData, documentData, docParam, conn) == false)
                                throw new Exception($"DocumentProcess: {respText}");

                            if (documentData?.DocDetail?.Any() == true)
                            {
                                documentData.DocDetail.ForEach(d =>
                                {
                                    var docDetail = new InventObject.DocDetail();
                                    if (_inventModule.DocDetailObj(ref respText, ref docDetail, d.DocDetailID, documentData.DocHeader.DocumentKey, d.MaterialID, "front", conn) == false)
                                        throw new Exception($"DocDetailObj: {respText}");

                                    var defaultUnit = docDetail.UnitList?.Where(u => u.UnitLargeID == docDetail.UnitLargeID).FirstOrDefault();
                                    if (defaultUnit == null)
                                        throw new Exception($"Not found unit for material {docDetail.MaterialID}");

                                    docDetail.UnitName = defaultUnit.UnitName;
                                    docDetail.UnitSmallID = defaultUnit.UnitSmallID;
                                    docDetail.UnitLargeID = defaultUnit.UnitLargeID;
                                    docDetail.UnitRatio = defaultUnit.UnitRatio.ToString();
                                    docDetail.UnitLargeRatio = defaultUnit.UnitLargeRatio.ToString();

                                    d = docDetail;
                                });

                                if (_inventModule.DocDetail_Add(ref respText, ref resultDocData, documentData.DocDetail, documentData.DocHeader, conn) == false)
                                    throw new Exception($"DocDetail_Add: {respText}");
                            }
                        }
                        return resultDocData;
                    });

                    return Ok(new
                    {
                        Status = HttpStatusCode.OK,
                        StatusCode = "200.201",
                        Data = docData
                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = HttpStatusCode.InternalServerError,
                    StatusCode = "500.500",
                    Message = ex.Message
                });
            }
        }

        [HttpGet]
        [Route("materials")]
        public async Task<IHttpActionResult> GetMaterials(int documentType = 0)
        {
            try
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
                        StatusCode = "200.200",
                        Data = materials
                    });
                }
            }
            catch (Exception ex)
            {

                return Ok(new
                {
                    Status = HttpStatusCode.InternalServerError,
                    StatusCode = "500.500",
                    Message = ex.Message
                });
            }
        }
    }
}
