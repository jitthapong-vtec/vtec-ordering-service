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

namespace VerticalTec.POS.Report.Dashboard.Controllers
{
    public class AuthenController : ApiControllerBase
    {
        IDbHelper _db;

        public AuthenController(IDbHelper db)
        {
            _db = db;
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
                        DataRow row = dtStaff.Rows[0];
                        result.Data = new
                        {
                            StaffID = row.GetValue<int>("StaffID"),
                            StaffRoleID = row.GetValue<int>("StaffRoleID"),
                            StaffName = $"{row.GetValue<string>("StaffFirstName")} {row.GetValue<string>("StaffLastName")}"
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
