using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace VerticalTec.POS.WebService.DataSync.Models
{
    public class ResponseBody<TResult>
    {
        [JsonProperty("httpcode")]
        public HttpStatusCode HttpCode { get; set; }
        [JsonProperty("data")]
        public TResult Data { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
