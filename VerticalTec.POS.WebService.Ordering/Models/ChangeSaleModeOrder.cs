using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VerticalTec.POS.WebService.Ordering.Models
{
    public class ChangeSaleModeOrder
    {
        public int ShopID { get; set; }
        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int OrderDetailID { get; set; }
        public SaleModes SaleMode { get; set; } = SaleModes.DineIn;
    }
}