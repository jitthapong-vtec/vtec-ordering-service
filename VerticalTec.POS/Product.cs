using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS
{
    public class Product
    {
        public int ProductId { get; set; }
        public int ProductDeptId { get; set; }
        public int ProductGroupId { get; set; }
        public int ParentProductId { get; set; }
        public int PGroupId { get; set; }
        public int SetGroupNo { get; set; }
        public SaleModes SaleMode { get; set; } = SaleModes.DineIn;
        public double UnitPrice { get; set; }
        public bool IsComponentProduct { get; set; }
    }
}
