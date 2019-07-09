using Owin;
using Swashbuckle.Application;
using System.Web.Http;
using System.Web.Http.Cors;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Models;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            HttpConfiguration config = new HttpConfiguration();

            var container = new UnityContainer();

            var dbServer = Config.GetDatabaseServer();
            var dbName = Config.GetDatabaseName();
            container.RegisterType<IDatabase, MySqlDatabase>(
                new ContainerControlledLifetimeManager(),
                new InjectionConstructor(dbServer, dbName, "3308"));
            container.RegisterSingleton<POSModule>();

            config.DependencyResolver = new UnityResolver(container);

            var cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

            config.EnableSwagger(c => c.SingleApiVersion("v1", "Vtec DataSync Api Interface")).EnableSwaggerUi();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Filters.Add(new GlobalExceptionHandler());
            config.MapHttpAttributeRoutes();

            appBuilder.UseWebApi(config);
        }
    }
}
