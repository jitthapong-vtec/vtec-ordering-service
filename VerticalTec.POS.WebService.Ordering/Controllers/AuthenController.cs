using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.WebService.Ordering.Controllers
{
    [Route("authen")]
    [ApiController]
    public class AuthenController : ControllerBase
    {
        IDatabase _db;
        VtecRepo _vtRepo;

        public AuthenController(IDatabase db)
        {
            _db = db;
            _vtRepo = new VtecRepo(db);
        }

        [HttpGet("device")]
        public async Task<ActionResult<object>> AuthenDevice(string code, int type = 2)
        {
            using (var conn = await _db.ConnectAsync())
            {
                var cmd = _db.CreateCommand(
                            " select * from computername where Deleted=0 and ComputerType=@type and DeviceCode=@deviceCode;" +
                            " select * from shop_data;" +
                            " select * from salemode where deleted=0;" +
                            " select * from programpropertyvalue;", conn);
                cmd.Parameters.Add(_db.CreateParameter("@type", type));
                cmd.Parameters.Add(_db.CreateParameter("@deviceCode", code));
                var adapter = _db.CreateDataAdapter(cmd);
                DataSet ds = new DataSet();
                adapter.TableMappings.Add("Table", "Device");
                adapter.TableMappings.Add("Table1", "ShopData");
                adapter.TableMappings.Add("Table2", "SaleMode");
                adapter.TableMappings.Add("Table3", "Property");
                adapter.Fill(ds);
                DataTable dtDevice = ds.Tables["Device"];
                if (dtDevice.Rows.Count > 0)
                {
                    var shopId = dtDevice.Rows[0].GetValue<int>("ShopID");
                    var deviceInfo = ds.Tables["Device"].Rows[0];
                    var shopInfo = ds.Tables["ShopData"].Rows[0];

                    var result = new
                    {
                        MerchantID = shopInfo.GetValue<int>("MerchantID"),
                        BrandID = shopInfo.GetValue<int>("BrandID"),
                        ShopID = shopInfo.GetValue<int>("ShopID"),
                        ComputerID = deviceInfo.GetValue<int>("ComputerID"),
                        ComputerName = deviceInfo.GetValue<string>("ComputerName"),
                        SaleModes = ds.Tables["SaleMode"].Select().Select(s => new
                        {
                            SaleModeID = s.GetValue<int>("SaleModeID"),
                            SaleModeName = s.GetValue<string>("SaleModeName")
                        }),
                        Properties = ds.Tables["Property"].Select("PropertyID in (10,12,13,14,15)").Select(p => new
                        {
                            PropertyID = p.GetValue<int>("PropertyID"),
                            PropertyValue = p.GetValue<int>("PropertyValue"),
                            PropertyTextValue = p.GetValue<string>("PropertyTextValue")
                        })
                    };

                    return result;
                }
                else
                {
                    return NotFound($"Not found registered device {code}");
                }
            }
        }

        [HttpPost("staff")]
        public async Task<ActionResult<object>> AuthenStaff(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return BadRequest("Please specify username and password");

            using (var conn = await _db.ConnectAsync())
            {
                var cmd = _db.CreateCommand("select * from staffs where StaffCode=@userName and StaffPassword=@password", conn);
                cmd.Parameters.Add(_db.CreateParameter("@userName", username));
                cmd.Parameters.Add(_db.CreateParameter("@password", HashUtil.SHA1(password)));

                var dtStaff = new DataTable();
                using (var reader = await _db.ExecuteReaderAsync(cmd))
                {
                    dtStaff.Load(reader);
                }
                if (dtStaff.Rows.Count > 0)
                {
                    DataRow row = dtStaff.Rows[0];
                    return new
                    {
                        StaffID = row.GetValue<int>("StaffID"),
                        StaffRoleID = row.GetValue<int>("StaffRoleID"),
                        StaffName = $"{row.GetValue<string>("StaffFirstName")} {row.GetValue<string>("StaffLastName")}"
                    };
                }
                else
                {
                    return NotFound($"Not found username {username}");
                }
            }
        }
    }
}