using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VerticalTec.POS.WebService.Ordering.Models
{
    public class LoyaltyApiResult<T1, T2, T3>
    {
        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("dataResult")]
        public T1 DataResult { get; set; }

        [JsonProperty("dataExtra")]
        public T2 DataExtra { get; set; }

        [JsonProperty("dataValue")]
        public T3 DataValue { get; set; }
    }
}