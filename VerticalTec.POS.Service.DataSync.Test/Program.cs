using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using VerticalTec.POS.Service.DataSync.Owin;
using VerticalTec.POS.Service.DataSync.Owin.Models;

namespace VerticalTec.POS.Service.DataSync.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://localhost:9001/";

            // Start OWIN host 
            using (WebApp.Start(baseAddress, appBuilder => new Startup("127.0.0.1", "srifa").Configuration(appBuilder)))
            {
                Task.Run(async () =>
                {
                    HttpClient client = new HttpClient();
                    var uri = baseAddress + "v1/sync/inv?docDate=&shopId=3";
                    for (int i = 0; i < 100; i++)
                    {
                        Console.WriteLine($"Send request #{i + 1}");
                        var respMessage = await client.GetAsync(uri);
                        var respContent = await respMessage.Content.ReadAsStringAsync();
                        var respBody = JsonConvert.DeserializeObject<ResponseBody<string>>(respContent);
                        if (respMessage.IsSuccessStatusCode)
                        {
                            if (respBody.Success)
                            {
                                Console.WriteLine($"Result #{i + 1} {respBody.Message}");
                            }
                            else
                            {
                                Console.WriteLine("All done!");
                                break;
                            }
                        }
                        else
                        {
                            Console.WriteLine(respBody.Message);
                            break;
                        }
                    }
                }).Wait();
                Console.ReadLine();
            }
        }
    }
}
