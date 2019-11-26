using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://+:9100/";

            try
            {
                using (WebApp.Start(baseAddress, appBuilder => new VerticalTec.POS.Service.DataSync.Owin.Startup("127.0.0.1", "srifa_sa").Configuration(appBuilder)))
                {
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
