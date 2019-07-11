using System;
using System.Configuration;
using System.IO;
using System.Web.Http;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.WebService.DataSync
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);

            try
            {
                var path = Server.MapPath("~/Log");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                LogManager.Instance.InitLogManager(path);
                var isEnableLog = Convert.ToBoolean(ConfigurationManager.AppSettings["EnableLog"]);
                LogManager.Instance.EnableLog = isEnableLog;
            }
            catch (Exception) { }
        }
    }
}
