using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using VerticalTec.POS.Service.DataSync.Owin;
using VerticalTec.POS.Service.DataSync.Owin.Models;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.DataSync.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            LogManager.Instance.InitLogManager("Log/");

            string baseAddress = "http://localhost:9001/";

            // Start OWIN host 
            using (WebApp.Start(baseAddress, appBuilder => new Startup("127.0.0.1", "srifa").Configuration(appBuilder)))
            {
                Task.Run(async () =>
                {
                    await TestSyncInv(baseAddress);
                });
                Console.ReadLine();
            }

            //Task.Run(async () =>
            //{
            //    var vdsclient = "http://127.0.0.1/v1/"; // from property 1012 parameter=vdsclientservice
            //    HttpClient client = new HttpClient();
            //    var uri = $"{vdsclient}commission/sendreceipt?shopId=3&tranId=203&compId=1";
            //    await client.GetAsync(uri);
            //});
        }

        private static async Task TestCommissionApi(string baseAddress)
        {
            HttpClient client = new HttpClient();
            var uri = baseAddress + "v1/commission/sendreceipt?shopId=3&tranId=196&compId=1";
            Console.WriteLine($"Send request {uri}");
            var respMessage = await client.GetAsync(uri);
            var respContent = await respMessage.Content.ReadAsStringAsync();
            var respBody = JsonConvert.DeserializeObject<ResponseBody<string>>(respContent);
            if (respMessage.IsSuccessStatusCode)
            {
                if (respBody.Success)
                {
                    Console.WriteLine($"Result: {respBody.Message}");
                }
                else
                {
                    Console.WriteLine("Done!");
                }
            }
            else
            {
                Console.WriteLine(respBody.Message);
            }
        }

        private static async Task TestSyncInv(string baseAddress)
        {
            HttpClient client = new HttpClient();
            var uri = baseAddress + "v1/inv/sendtohq?docDate=&shopId=4";
            //var uri = baseAddress + "v1/sale/sendtohq?shopid=4";
            //for (int i = 0; i < 100; i++)
            //{
            //    Console.WriteLine($"Send request #{i + 1}");
            //    var respMessage = await client.GetAsync(uri);
            //    var respContent = await respMessage.Content.ReadAsStringAsync();
            //    var respBody = JsonConvert.DeserializeObject<ResponseBody<string>>(respContent);
            //    if (respMessage.IsSuccessStatusCode)
            //    {
            //        if (respBody.Success)
            //        {
            //            Console.WriteLine($"Result #{i + 1} {respBody.Message}");
            //        }
            //        else
            //        {
            //            Console.WriteLine("All done!");
            //            break;
            //        }
            //    }
            //    else
            //    {
            //        Console.WriteLine(respBody.Message);
            //        break;
            //    }
            //}

            Console.WriteLine($"Send request {uri}");
            var respMessage = await client.GetAsync(uri);
            var respContent = await respMessage.Content.ReadAsStringAsync();
            var respBody = JsonConvert.DeserializeObject<ResponseBody<string>>(respContent);
            if (respMessage.IsSuccessStatusCode)
            {
                if (respBody.Success)
                {
                    Console.WriteLine($"Result: {respBody.Message}");
                }
                else
                {
                    Console.WriteLine("All done!");
                }
            }
            else
            {
                Console.WriteLine(respBody.Message);
            }
        }
    }
}
