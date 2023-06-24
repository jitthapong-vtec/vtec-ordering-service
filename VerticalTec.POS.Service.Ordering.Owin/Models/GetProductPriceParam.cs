using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class GetProductPriceParam
    {
        public string[] ProductCodes { get; set; }
        public int?[] ProductIds { get; set; }
    }
}
