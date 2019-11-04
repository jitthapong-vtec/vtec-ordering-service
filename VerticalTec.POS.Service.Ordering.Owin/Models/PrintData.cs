using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class PrintData
    {
        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int ShopID { get; set; }
        public int StaffID { get; set; }
        public int LangID { get; set; } = 1;
        public int PaperSize { get; set; } = 80;
        public string PrinterIds { get; set; } = "";
        public string PrinterNames { get; set; } = "CASHIER";
    }
}