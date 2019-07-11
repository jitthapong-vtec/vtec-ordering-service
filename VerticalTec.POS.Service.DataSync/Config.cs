using System;
using System.Configuration;

namespace VerticalTec.POS.Service.DataSync
{
    public class Config
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

        public static string GetPort()
        {
            var config = ConfigurationManager.OpenExeConfiguration(GetExecPath());
            return config.AppSettings.Settings["Port"].Value;
        }

        public static bool IsEnableLog()
        {
            var config = ConfigurationManager.OpenExeConfiguration(GetExecPath());
            return Convert.ToBoolean(config.AppSettings.Settings["EnableLog"].Value);
        }

        public static string GetExecPath()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().Location;
        }
    }
}
