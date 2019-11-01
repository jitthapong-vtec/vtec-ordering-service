namespace VerticalTec.POS
{
    public abstract class ProductBase
    {
        public int ProductID { get; set; }
        public int ParentProductID { get; set; }
        public int ProductGroupID { get; set; }
        public int ProductDeptID { get; set; }
        public int ProductTypeID { get; set; }
        public int OtherProductGroupID { get; set; }
        public int OtherProductTypeID { get; set; }
        public string ProductCode { get; set; }
        public decimal ProductPrice { get; set; }
        public string PN { get; set; }
        public string ProductName { get; set; }
        public string ProductName1 { get; set; }
        public string ProductName2 { get; set; }
        public string ProductName3 { get; set; }
        public string ProductDisplayName { get; set; }
        public string ProductImage { get; set; }
        public int AutoComment { get; set; }
        public bool IsComponentProduct { get; set; }
        public int RequireAddAmount { get; set; }
        public bool IsComment { get => ProductTypeID == 14 || ProductTypeID == 15; }
        public bool NormalProduct { get => ProductTypeID == 0; }
        public bool ProductSet { get => ProductTypeID == 1; }
        public bool ProductSize { get => ProductTypeID == 2; }
        public bool ComboSetProduct { get => ProductTypeID == 7; }
        public bool Modifier { get => ProductTypeID == 14; }
        public bool ModifierWithPrice { get => ProductTypeID == 15; }
        public bool Deleted { get; set; }
    }
}
