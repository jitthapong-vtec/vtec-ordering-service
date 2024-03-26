using Newtonsoft.Json;
using System;
using System.Globalization;

namespace VerticalTec.POS
{
    public class Transaction
    {
        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int ShopID { get; set; }
        public int TerminalID { get; set; }
        public int TableID { get; set; }
        public int MemberID { get; set; }
        public int StaffID { get; set; }
        public int LangID { get; set; } = 1;
        public int ProcessType { get; set; }
        [JsonIgnore]
        public string ShopCode { get; set; }
        public SaleModes SaleMode { get; set; } = SaleModes.DineIn;
        public string TableName { get; set; }
        public string TransactionName { get; set; }
        public string QueueName { get; set; }
        public int TotalCustomer { get; set; } = 1;
        [JsonIgnore]
        public string SaleDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public string PrinterNames { get; set; } = "";
        public string PrinterIds { get; set; } = "";
        public int PaperSize { get; set; } = 80;
        public int PrinterId { get; set; }
        public string QueueNo { get; set; }
        public decimal ReceiptPayPrice { get; set; }
        public double ReceiptTotalQty { get; set; }
        [JsonIgnore]
        public bool NeedInputQueue { get; set; }
        public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.New;
    }
}
