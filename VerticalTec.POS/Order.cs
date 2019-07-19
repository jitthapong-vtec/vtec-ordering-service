using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS
{
    public class Order : Product
    {
        public int TransactionId { get; set; }
        public int ComputerId { get; set; }
        public int ShopId { get; set; }
        public int OrderDetailId { get; set; }
        public int OrderDetailLinkId { get; set; }
        public int IndentLevel { get; set; }
        public string OtherFoodName { get; set; } = "";
        public int OtherProductGroupId { get; set; }
        public string OtherPrinterId { get; set; }
        public int OtherDiscountAllow { get; set; }
        public int OtherInventoryId { get; set; }
        public int OtherVatType { get; set; }
        public int OtherPrintGroup { get; set; }
        public string OtherProductVatCode { get; set; } = "";
        public int OtherHasSc { get; set; }
        public int OtherProductTypeId { get; set; }
        public int StaffId { get; set; }
        public int TableId { get; set; }
        public double QtyRatio { get; set; }
        public double TotalQty { get; set; } = 1;
        public double OpenPrice { get; set; }
    }
}
