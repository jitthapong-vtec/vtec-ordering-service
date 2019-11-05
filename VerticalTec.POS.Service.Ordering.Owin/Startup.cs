using Hangfire;
using Hangfire.LiteDB;
using Owin;
using Swashbuckle.Application;
using System;
using System.Collections.Generic;
using System.IO;
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
using VerticalTec.POS.Service.Ordering.Owin.Services;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.Ordering.Owin
{
    public class Startup
    {
        IUnityContainer _container;

        public Startup(string dbServer, string dbName)
        {
            AppConfig.Instance.DbServer = dbServer;
            AppConfig.Instance.DbName = dbName; 
            NLog.LogManager.LoadConfiguration(string.Concat(Directory.GetCurrentDirectory(), "/nlog.config"));
        }

        private IEnumerable<IDisposable> GetHangfireServers()
        {
            GlobalConfiguration.Configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseUnityActivator(_container)
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

            _container = new UnityContainer();
            _container.RegisterType<IDatabase, MySqlDatabase>(new TransientLifetimeManager(), 
                new InjectionConstructor(AppConfig.Instance.DbServer, 
                AppConfig.Instance.DbName, 
                AppConfig.Instance.DbPort));
            _container.RegisterType<IOrderingService, OrderingService>(new TransientLifetimeManager());
            _container.RegisterSingleton<ILogService, LogService>();
            _container.RegisterSingleton<IMessengerService, MessengerService>();
            _container.RegisterSingleton<IPrintService, PrintService>();
            config.DependencyResolver = new UnityResolver(_container);

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
