using Newtonsoft.Json;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class OrderPromotion
    {
        public int TransactionID { get; set; }
        public int ComputerID { get; set; }
        public int TerminalID { get; set; }
        public int ShopID { get; set; }
        public int StaffID { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int MemberID { get; set; }
        public string VoucherSn { get; set; }
        public VoucherData VoucherData { get; set; }
    }
}