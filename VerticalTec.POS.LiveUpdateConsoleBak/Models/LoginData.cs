using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdateConsole.Models
{
    public class LoginData
    {
        [Required(ErrorMessage = "Username is required")]
        public string UserName { get; set; }
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }

        public int StaffId { get; set; }
        public int StaffRoleId { get; set; }
        public string StaffFirstName { get; set; }
        public string StaffLastName { get; set; }
    }
}
