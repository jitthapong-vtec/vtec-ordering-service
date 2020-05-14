using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VerticalTec.POS.LiveUpdateConsole.Models
{
    public class ShopData
    {
        public int BrandId { get; set; }
        public int ShopId { get; set; }
        public string ShopName { get; set; }
        public bool Selected { get; set; }
    }
}
