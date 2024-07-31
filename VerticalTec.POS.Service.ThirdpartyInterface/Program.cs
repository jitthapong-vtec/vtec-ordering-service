using System;
using System.ServiceProcess;

namespace VerticalTec.POS.Service.ThirdpartyInterface
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("ThirdpartyInterface") { FileName = $"Log/ThirdpartyInterface_{DateTime.Today.ToString("yyyy-MM-dd")}.log" };
            logfile.Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=tostring}";
            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logfile);

            NLog.LogManager.Configuration = config;

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new ThirdpartyOrderInterfaceService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
