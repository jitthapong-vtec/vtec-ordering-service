using System;
using System.Configuration;

namespace VerticalTec.POS.Service.ThirdpartyInterface
{
    public class ServiceConfig
    {
        public static string GetDatabaseServer()
        {
            var config = ConfigurationManager.OpenExeConfiguration(GetExecPath());
            return config.AppSettings.Settings["DBServer"].Value;
        }

        public static string GetDatabaseName()
        {
            var config = ConfigurationManager.OpenExeConfiguration(GetExecPath());
            return config.AppSettings.Settings["DBName"].Value;
        }

        public static string GetExecPath()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().Location;
        }
    }
}
