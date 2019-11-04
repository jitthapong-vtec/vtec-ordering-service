using Hangfire;
using Hangfire.LiteDB;
using Owin;
using Swashbuckle.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.Ordering.Owin.Models;

namespace VerticalTec.POS.Service.Ordering.Owin
{
    public class Startup
    {
        public Startup(string dbServer, string dbName)
        {
            AppConfig.Instance.DbServer = dbServer;
            AppConfig.Instance.DbName = dbName;
        }

        private IEnumerable<IDisposable> GetHangfireServers()
        {
            GlobalConfiguration.Configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseLiteDbStorage("Hangfire.db");

            yield return new BackgroundJobServer();
        }

        public void Configuration(IAppBuilder appBuilder)
        {
            HttpConfiguration config = new HttpConfiguration();
            var cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

            var container = new UnityContainer();
            container.RegisterType<IDatabase, MySqlDatabase>(new TransientLifetimeManager(), 
                new InjectionConstructor(AppConfig.Instance.DbServer, AppConfig.Instance.DbName, "3308"));
            container.RegisterType<IOrderingService, OrderingService>();
            config.DependencyResolver = new UnityResolver(container);

            config.EnableSwagger(c => c.SingleApiVersion("v1", "Vtec Ordering Api")).EnableSwaggerUi();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Filters.Add(new GlobalExceptionHandler());
            config.MapHttpAttributeRoutes();

            appBuilder.UseHangfireAspNet(GetHangfireServers);
            appBuilder.UseHangfireDashboard();
            appBuilder.UseWebApi(config);
        }
    }
}
