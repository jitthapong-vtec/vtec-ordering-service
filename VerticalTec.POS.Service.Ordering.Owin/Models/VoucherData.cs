using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class VoucherData
    {
        [JsonProperty("voucherSN")]
        public string VoucherSn { get; set; }
        [JsonProperty("voucherUDDID")]
        public string VoucherUDDID { get; set; }
        [JsonProperty("voucherId")]
        public int VoucherId { get; set; }
        [JsonProperty("voucherShopId")]
        public int VoucherShopId { get; set; }
        [JsonProperty("voucherTypeId")]
        public int VoucherTypeId { get; set; }
        [JsonProperty("voucherNo")]
        public string VoucherNo { get; set; }
        [JsonProperty("voucherHeaderId")]
        public int VoucherHeaderId { get; set; }
        [JsonProperty("voucherHeader")]
        public string VoucherHeader { get; set; }
        [JsonProperty("voucherName")]
        public string VoucherName { get; set; }
        [JsonProperty("promotionCode")]
        public string PromotionCode { get; set; }
        [JsonProperty("voucherStatus")]
        public int VoucherStatus { get; set; }
        [JsonProperty("voucherPrice")]
        public double VoucherPrice { get; set; }
        [JsonProperty("refCardId")]
        public int RefCardId { get; set; }
        [JsonProperty("activateDate")]
        public string ActivateDate { get; set; }
        [JsonProperty("expireDate")]
        public string ExpireDate { get; set; }
        [JsonProperty("imgTextColor")]
        public int ImgTextColor { get; set; }
        [JsonProperty("payTypeId")]
        public int PayTypeID { get; set; }
        [JsonProperty("memberId")]
        public int MemberID { get; set; }
        [JsonProperty("memberCode")]
        public string MemberCode { get; set; }
        [JsonProperty("memberName")]
        public string MemberName { get; set; }
    }
}