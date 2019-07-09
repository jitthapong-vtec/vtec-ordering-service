using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Service.DataSync.Models;

namespace VerticalTec.POS.Service.DataSync.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://localhost:9000/";

            // Start OWIN host 
            using (WebApp.Start<Startup>(url: baseAddress))
            {
                var uri = baseAddress + "v1/sync/inv?docDate=2019-07-09&shopId=3";
                Console.WriteLine("Send request " + uri);
                HttpClient client = new HttpClient();
                var respMessage = client.GetAsync(uri).Result;
                var respContent = respMessage.Content.ReadAsStringAsync().Result;
                var respBody = JsonConvert.DeserializeObject<ResponseBody<string>>(respContent);
                Console.WriteLine("Result " + respBody.Message);
                Console.ReadLine();
            }
        }
    }
}
