using System;
using System.Configuration;

namespace VerticalTec.POS.Service.Test
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

        public static string GetApiPort()
        {
            var config = ConfigurationManager.OpenExeConfiguration(GetExecPath());
            return config.AppSettings.Settings["ApiPort"].Value;
        }

        public static string GetApiUser()
        {
            var config = ConfigurationManager.OpenExeConfiguration(GetExecPath());
            return config.AppSettings.Settings["ApiUser"].Value;
        }

        public static string GetApiPassword()
        {
            var config = ConfigurationManager.OpenExeConfiguration(GetExecPath());
            return config.AppSettings.Settings["ApiPass"].Value;
        }

        public static string GetExecPath()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().Location;
        }
    }
}
