using System;
using System.Globalization;
using VerticalTec.POS.Restaurant.Core.Extensions;

namespace VerticalTec.POS
{
    public class PaymentData
    {
        public int PayTypeID { get; set; }
        public int PayDetailID { get; set; }
        public int EDCType { get; set; }
        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int TerminalID { get; set; }
        public int ShopID { get; set; }
        public int StoreID { get; set; }
        public string BrandName { get; set; }
        public int BankNameID { get; set; } = 0;
        public int CreditCardType { get; set; } = 0;
        public string CreditCardNo { get; set; }
        public int StaffID { get; set; } = 1;
        public int LangID { get; set; } = 1;
        public string SaleDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public int CurrencyID { get; set; } = 1;
        public string CurrencyCode { get; set; } = "";
        public string CurrencyName { get; set; } = "";
        public double CurrencyRatio { get; set; } = 1;
        public double ExchangeRate { get; set; } = 1;
        public string PayTypeName { get; set; } = "";
        public string Remark { get; set; } = "";
        public string PrinterIds { get; set; }
        public string PrinterNames { get; set; }
        public int PaperSize { get; set; } = 80;
        public int WalletType { get; set; } = 1;
        public string ExtraParam { get; set; } = "";
        public string TableName { get; set; } = "";
        public string WalletStoreId { get; set; }
        public string WalletDeviceId { get; set; }
        public string WalletTypeName { get; set; } = "";
        public string CustAccountNo { get; set; } = "";
        public string CardHolderName { get; set; }
        public string CardExpMonth { get; set; }
        public string CardExpYear { get; set; }
        public string ReferenceNo { get; set; }
        public string EncryptedCardInfo { get; set; }
        public decimal PayAmount { get; set; }
        public decimal CashChange { get; set; }
        public decimal CurrencyAmount { get; set; }
        public decimal CashChangeMainCurrency { get; set; }
        public decimal CashChangeCurrencyAmount { get; set; }
    }
}
