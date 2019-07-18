using System;
using System.Configuration;
using System.Web.Http;
using System.Web.Http.Cors;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using VerticalTec.POS.Database;
using VerticalTec.POS.WebService.DataSync.Models;
using vtecPOS_SQL.POSControl;

namespace VerticalTec.POS.WebService.DataSync
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            var container = new UnityContainer();

            var dbServer = ConfigurationManager.AppSettings["DBServer"];
            var dbName = ConfigurationManager.AppSettings["DBName"];
            container.RegisterType<IDatabase, SqlServerDatabase>(
                new ContainerControlledLifetimeManager(),
                new InjectionConstructor(dbServer, dbName));
            container.RegisterSingleton<POSModule>();

            config.DependencyResolver = new UnityResolver(container);

            var cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Filters.Add(new GlobalExceptionHandler());
            config.MapHttpAttributeRoutes();
        }
    }
}
