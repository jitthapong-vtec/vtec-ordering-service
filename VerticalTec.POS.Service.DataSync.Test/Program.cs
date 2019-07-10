using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using VerticalTec.POS.Service.DataSync.Owin;
using VerticalTec.POS.Service.DataSync.Owin.Models;

namespace VerticalTec.POS.Service.DataSync.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://localhost:9000/";

            // Start OWIN host 
            using (WebApp.Start(baseAddress, appBuilder => new Startup("127.0.0.1", "srifa").Configuration(appBuilder)))
            {
                var uri = baseAddress + "v1/sync/inv?docDate=&shopId=3";
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
