using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Report.Dashboard.Models;
using vtecdbhelper;
using VerticalTec.POS.Utils;
using VerticalTec.POS.Database;

namespace VerticalTec.POS.Report.Dashboard.Controllers
{
    public class AuthenController : ApiControllerBase
    {
        IDbHelper _db;
        IDatabase _db2;
        VtecPOSRepo _posRepo;

        public AuthenController(IDbHelper db, IDatabase db2)
        {
            _db = db;
            _db2 = db2;
            _posRepo = new VtecPOSRepo(db2);
        }

        [HttpPost]
        [ActionName("login")]
        public async Task<IActionResult> LoginAsync(UserLogin payload)
        {
            var result = new ReportActionResult<object>();
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var dtProp = await _posRepo.GetProgramPropertyAsync(conn);
                    var properies = new List<object>();
                    foreach(var prop in dtProp.Select())
                    {
                        properies.Add(new {
                            PropertyId = prop.GetValue<int>("PropertyID"),
                            PropertyValue = prop.GetValue<int>("PropertyValue"),
                            PropertyTextValue = prop.GetValue<string>("PropertyTextValue")
                        });
                    }
                    var cmd = _db.CreateCommand("select * from staffs where StaffCode=@userName and StaffPassword=@password", conn);
                    cmd.Parameters.Add(_db.CreateParameter("@userName", payload.Username ?? ""));
                    cmd.Parameters.Add(_db.CreateParameter("@password", HashUtil.SHA1(payload.Password ?? "")));

                    var dtStaff = new DataTable();
                    using (var reader = await _db.ExecuteReaderAsync(cmd))
                    {
                        dtStaff.Load(reader);
                    }
                    if (dtStaff.Rows.Count > 0)
                    {
                        DataRow staffRow = dtStaff.Rows[0];
                        result.Data = new
                        {
                            StaffData = new {
                                StaffId = staffRow.GetValue<int>("StaffID"),
                                StaffRoleId = staffRow.GetValue<int>("StaffRoleID"),
                                StaffName = $"{staffRow.GetValue<string>("StaffFirstName")} {staffRow.GetValue<string>("StaffLastName")}"
                            },
                            Properties = properies
                        };
                    }
                    else
                    {
                        result.StatusCode = StatusCodes.Status401Unauthorized;
                        result.Message = $"Login fail for {payload.Username}";
                    }

                }
            }
            catch (Exception ex)
            {
                result.StatusCode = StatusCodes.Status500InternalServerError;
                result.Message = ex.Message;
            }
            return result;
        }
    }
}
