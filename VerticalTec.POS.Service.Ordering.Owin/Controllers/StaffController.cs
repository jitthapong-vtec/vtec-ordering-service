using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;
using VerticalTec.POS.Service.Ordering.Owin.Models;
using MySql.Data.MySqlClient;
using System;

namespace VerticalTec.POS.Service.Ordering.Owin.Controllers
{
    public class StaffController : ApiController
    {
        public static readonly object Owner = new object();

        IDatabase _database;
        VtecPOSRepo _posRepo;

        public StaffController(IDatabase database)
        {
            _database = database;
            _posRepo = new VtecPOSRepo(database);
        }

        [HttpPost]
        [Route("v1/staffs/logout")]
        public async Task<IHttpActionResult> Logout(int staffId, int shopId, int terminalId)
        {
            var result = new HttpActionResult<object>(Request);
            using (IDbConnection conn = await _database.ConnectAsync())
            {
                var cmd = new MySqlCommand("delete from computeraccess where LastLoginStaffID=@staffId and ShopID=@shopId and ComputerID=@terminalId", (MySqlConnection)conn);
                cmd.Parameters.Clear();
                cmd.Parameters.AddRange(new MySqlParameter[]
                {
                    new MySqlParameter("@staffId", staffId),
                    new MySqlParameter("@shopId", shopId),
                    new MySqlParameter("@terminalId", terminalId)
                });
                await cmd.ExecuteNonQueryAsync();
            }
            return result;
        }

        [HttpPost]
        [Route("v1/staffs/identify")]
        public IHttpActionResult IdentifyStaff(string staffCode = "", string password = "", int shopId = 0, int terminalId = 0)
        {
            lock (Owner)
            {
                var result = new HttpActionResult<object>(Request);
                using (IDbConnection conn = _database.ConnectAsync().Result)
                {
                    var dtStaff = _posRepo.GetStaffAsync(conn, staffCode, password).Result;
                    var dtStaffPermission = _posRepo.GetStaffPermissionAsync(conn).Result;

                    if (dtStaff.Rows.Count > 0)
                    {
                        var staffId = dtStaff.Rows[0].GetValue<int>("StaffID");
                        var allowAccess = true;
                        if (shopId > 0)
                            allowAccess = _posRepo.CheckStaffAccessShop(conn, staffId, shopId).Result;

                        if (allowAccess)
                        {
                            var staff = (from row in dtStaff.AsEnumerable()
                                         select new
                                         {
                                             StaffID = row.GetValue<int>("StaffID"),
                                             StaffRoleID = row.GetValue<int>("StaffRoleID"),
                                             StaffFirstName = row.GetValue<string>("StaffFirstName"),
                                             StaffLastName = row.GetValue<string>("StaffLastName"),
                                             LangID = row.GetValue<int>("LangID"),
                                             Permissions = (from permission in dtStaffPermission.Select($"StaffRoleID={row.GetValue<int>("StaffRoleID")}")
                                                            select new
                                                            {
                                                                PermissionItemID = permission.GetValue<int>("PermissionItemID")
                                                            }).ToList()
                                         }).FirstOrDefault();


                            var dtProperty = _posRepo.GetProgramPropertyAsync(conn, 1097).Result;
                            var isSingleLogin = dtProperty.AsEnumerable().Select(r => r.GetValue<int>("PropertyValue") == 1).FirstOrDefault();
                            if (isSingleLogin)
                            {
                                var dtComputerAccess = new DataTable();
                                var cmd = new MySqlCommand("select a.*, b.ComputerName from computeraccess a" +
                                    " join ComputerName b " +
                                    " on a.ComputerID=b.ComputerID" +
                                    " where a.LastLoginStaffID=@staffId and a.ComputerID != @terminalId", (MySqlConnection)conn);
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddRange(new MySqlParameter[]
                                {
                                new MySqlParameter("@staffId", staff.StaffID),
                                new MySqlParameter("@terminalId", terminalId)
                                });
                                using (var reader = cmd.ExecuteReader())
                                {
                                    dtComputerAccess.Load(reader);
                                }
                                if (dtComputerAccess.Rows.Count > 0)
                                {
                                    var row = dtComputerAccess.AsEnumerable().First();
                                    result.StatusCode = HttpStatusCode.OK;
                                    result.Body = new
                                    {
                                        Code = "ACCESS_ANOTHER_COMPUTER",
                                        ComputerName = row["ComputerName"]
                                    };
                                }
                                else
                                {
                                    cmd = new MySqlCommand("delete from computeraccess where ShopID=@shopId and ComputerID=@terminalId", (MySqlConnection)conn);
                                    cmd.Parameters.Clear();
                                    cmd.Parameters.AddRange(new MySqlParameter[]
                                    {
                                    new MySqlParameter("@shopId", shopId),
                                    new MySqlParameter("@terminalId", terminalId)
                                    });
                                    cmd.ExecuteNonQuery();

                                    cmd = new MySqlCommand("insert into computeraccess(ComputerID,ShopID,StockToInvID,LastLoginStaffID) values (@terminalId,@shopId,@shopId,@staffId)", (MySqlConnection)conn);
                                    cmd.Parameters.AddRange(new MySqlParameter[]
                                    {
                                    new MySqlParameter("@shopId", shopId),
                                    new MySqlParameter("@terminalId", terminalId),
                                    new MySqlParameter("@staffId", staff.StaffID)
                                    });
                                    cmd.ExecuteNonQuery();

                                    result.StatusCode = HttpStatusCode.OK;
                                    result.Body = staff;
                                }
                            }
                            else
                            {
                                result.StatusCode = HttpStatusCode.OK;
                                result.Body = staff;
                            }
                        }
                        else
                        {
                            result.StatusCode = HttpStatusCode.Unauthorized;
                        }
                    }
                    else
                    {
                        result.StatusCode = HttpStatusCode.NotFound;
                    }
                }
                return result;
            }
        }
    }
}
