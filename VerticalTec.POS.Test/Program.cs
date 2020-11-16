using RestSharp;
using System;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var restClient = new RestClient();

            // order object
            var order = new POSObject.OrderObj();

            // create request 
            var request = new RestRequest("http://127.0.0.1:9500/v1/orders/online", Method.POST);
            request.AddJsonBody(order);

            // execute request
            var response = await restClient.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
            }
            else
            {
                var errMsg = response.Content;
            }
            Console.ReadLine();
        }
    }
}
