using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Core
{
    public class Order
    {
        public int TransactionId { get; set; }
        public int ComputerId { get; set; }
        public int OrderDetailId { get; set; }
        public int OrderDetailLinkId { get; set; }
        public int IndentLevel { get; set; }
        public int ProductId { get; set; }
        public int ParentProductId { get; set; }
        public int PGroupId { get; set; }
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
        public int SetGroupNo { get; set; }
        public double QtyRatio { get; set; }
        public SaleModes SaleMode { get; set; } = SaleModes.DineIn;
        public double TotalQty { get; set; }
        public double OpenPrice { get; set; }
        public int ShopId { get; set; }
        public bool IsComponentProduct { get; set; }
    }
}
