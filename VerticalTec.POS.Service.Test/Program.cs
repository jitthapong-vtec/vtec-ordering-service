using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Service.Ordering.Owin;

namespace VerticalTec.POS.Service.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://+:9100/";

            try
            {
                using (WebApp.Start(baseAddress, appBuilder => new Startup("127.0.0.1", "hny", "").Configuration(appBuilder)))
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
