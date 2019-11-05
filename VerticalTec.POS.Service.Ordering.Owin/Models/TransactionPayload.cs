using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class TransactionPayload
    {
        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int TerminalID { get; set; }
        public int ShopID { get; set; }
        public int StaffID { get; set; }
        public int TableID { get; set; }
        public int LangID { get; set; } = 1;
        public int TotalCustomer { get; set; } = 1;
        public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.New;
        public string TransactionName { get; set; } = "";
        public string TableName { get; set; } = "";
        public string PrinterIds { get; set; } = "";
        public string PrinterNames { get; set; } = "";
    }
}
