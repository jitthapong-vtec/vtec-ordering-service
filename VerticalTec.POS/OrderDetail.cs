using Newtonsoft.Json;
using VerticalTec.POS;

namespace VerticalTec.POS
{
    public class OrderDetail : ProductBase
    {
        bool _isParentOrder;
        double? _currentStock;
        bool _enableModifyQty = true;

        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int TerminalID { get; set; }
        public int OrderDetailID { get; set; }
        public int OrderDetailLinkID { get; set; }
        public int OrderStatusID { get; set; }
        public int IndentLevel { get; set; }
        public int PGroupID { get; set; }
        public int ProductSetType { get; set; }
        public decimal TotalRetailPrice { get; set; }
        public int SetGroupNo { get; set; }
        public SaleModes SaleMode { get; set; } = SaleModes.DineIn;
        [JsonIgnore]
        internal bool RefreshPromo { get; set; }
        public double FromQty { get; set; }
        public double ToQty { get; set; }
        public double OpenPrice { get; set; }
        [JsonIgnore]
        internal int DecimalDigit { get; set; }
        [JsonIgnore]
        internal string SaleDate { get; set; }
        public int ShopID { get; set; }
        public string OtherFoodName { get; set; } = "";
        [JsonIgnore]
        internal string OtherPrinterID { get; set; } = "";
        [JsonIgnore]
        internal int OtherDiscountAllow { get; set; }
        [JsonIgnore]
        internal int OtherInventoryID { get; set; }
        [JsonIgnore]
        internal int OtherVatType { get; set; }
        [JsonIgnore]
        internal int OtherPrintGroup { get; set; }
        [JsonIgnore]
        internal string OtherProductVatCode { get; set; } = "";
        [JsonIgnore]
        internal int OtherHasSc { get; set; }
        public int OrderStaffID { get; set; }
        public int OrderComputerID { get; set; }
        public int OrderTableID { get; set; }
        public double QtyRatio { get; set; } = 1;
        [JsonIgnore]
        internal int FreeItem { get; set; }
        [JsonIgnore]
        internal string ModifyReasonIdList { get; set; }
        [JsonIgnore]
        internal string ModifyReasonText { get; set; }
        [JsonIgnore]
        internal string Details { get; set; } = "";
        public int PrintStatus { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal SalePrice { get; set; }
        public string VATDisplay { get; set; }
        public int VATType { get; set; }
        public decimal NetSale { get; set; }
        public double ProductVAT { get; set; }
        public decimal ProductBeforeVAT { get; set; }
        public decimal ServiceCharge { get; set; }
        public double ServiceChargeVAT { get; set; }
        public decimal ServiceChargeBeforeVAT { get; set; }
        public decimal Vatable { get; set; }
        public int ItemDiscAllow { get; set; }
        /// <summary>
        /// Flag for enable/disable delete button on Kiosk
        /// </summary>
        public bool EnableDelete { get; set; } = true;

        public bool IsParentOrder
        {
            get
            {
                if (ComboSetProduct)
                    return true;
                return _isParentOrder;
            }
            set
            {
                _isParentOrder = value;
            }
        }

        public double TotalQty { get; set; }

        public double MoveQty { get; set; }

        public double? CurrentStock
        {
            get
            {
                return _currentStock;
            }
            set
            {
                _currentStock = value;

                if (EnableCountDownStock)
                {
                    if (CurrentStock == 0)
                        OutOfStock = true;
                    else
                        OutOfStock = false;
                }
                else
                {
                    OutOfStock = false;
                }
            }
        }

        public bool EnableCountDownStock { get; set; }

        public bool EnableModifyQty
        {
            get
            {
                if (!_enableModifyQty)
                    return _enableModifyQty;
                else
                    return ComboSetProduct ? false : true;
            }
            set
            {
                _enableModifyQty = value;
            }
        }

        public bool OutOfStock { get; set; }
    }
}
