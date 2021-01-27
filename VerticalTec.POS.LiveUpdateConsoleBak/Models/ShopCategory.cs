using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdateConsole.Models
{
    public class ShopCategory
    {
        public int ShopCateId { get; set; }
        public string ShopCateCode { get; set; }
        public string ShopCateName { get; set; }
        public bool Selected { get; set; }
    }
}
