using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS
{
    public class TableManage
    {
        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int TerminalID { get; set; }
        public int FromTableID { get; set; }
        public int ToTableID { get; set; }
        public string ToTableIds { get; set; }
        public string ReasonList { get; set; }
        public string ReasonText { get; set; }
        public int StaffID { get; set; }
        public int ShopID { get; set; }
        public int LangID { get; set; }
        public SaleModes SaleMode { get; set; }
        public List<OrderDetailMove> Orders { get; set; }
    }

    public class OrderDetailMove
    {
        public int OrderDetailID { get; set; }
        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int OrderDetailLinkID { get; set; }
        public double MoveQty { get; set; }
    }
}
