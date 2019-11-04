using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class GrcPayload
    {
        [JsonProperty("order_id")]
        public string OrderID { get; set; }
        [JsonProperty("shop_id")]
        public int ShopID { get; set; }
        [JsonProperty("amount")]
        public int Amount { get; set; }
        [JsonProperty("computer_id")]
        public int ComputerID { get; set; }
        [JsonProperty("fintech")]
        public string Fintech { get; set; }
        [JsonProperty("customer_account")]
        public string CustomerAccount { get; set; }
    }
}