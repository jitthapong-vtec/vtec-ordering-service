using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class TransactionPayload
    {
        public int TransactionId { get; set; }
        public int ComputerId { get; set; }
        public int TerminalId { get; set; }
        public int ShopId { get; set; }
        public int StaffId { get; set; }
        public int LangId { get; set; } = 1;
        public string PrinterIds { get; set; }
        public string PrinterNames { get; set; }
    }
}
