namespace VerticalTec.POS
{
    public class PaymentCurrency
    {
        public int CurrencyID { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencyName { get; set; }
        public string CountryName { get; set; }
        public int IsMainCurrency { get; set; }
        public int IsChangeCurrency { get; set; }
        public int Ordering { get; set; }
        public int Activated { get; set; }
        public int Deleted { get; set; }
        public int GroupID { get; set; }
        public double CurrencyRatio { get; set; }
        public double ExchangeRate { get; set; }
        public double ChangeExchangeRate { get; set; }
    }
}
