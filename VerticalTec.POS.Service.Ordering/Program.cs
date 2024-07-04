using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("orderingservice") { FileName = $"Log/orderingservice_{DateTime.Today.ToString("yyyy-MM-dd")}.log"  };
            logfile.Layout = "${longdate}|${level:uppercase=true}|${logger}|${threadid}|${message}|${exception:format=tostring}";
            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logfile);
            
            NLog.LogManager.Configuration = config;

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new VtecOrderingService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
