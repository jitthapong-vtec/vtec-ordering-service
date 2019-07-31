using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VerticalTec.POS.Report.Dashboard.Models
{
    public class ResponseBody<TResult>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        [JsonProperty("data")]
        public TResult Data { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
