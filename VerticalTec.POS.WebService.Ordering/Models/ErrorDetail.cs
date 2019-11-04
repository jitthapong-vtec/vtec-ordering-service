using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VerticalTec.POS.WebService.Ordering.Models
{
    public class ErrorDetail
    {
            [JsonProperty("errcode", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public ErrorCodes ErrCode { get; set; }
            [JsonProperty("message")]
            public string Message { get; set; }
            [JsonProperty("detail", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string Detail { get; set; }
    }
}
