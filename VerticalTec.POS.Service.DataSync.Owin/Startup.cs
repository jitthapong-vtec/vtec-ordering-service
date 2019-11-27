using Hangfire;
using Hangfire.LiteDB;
using Owin;
using Swashbuckle.Application;
using System;
using System.Collections.Generic;
using System.Web.Http;
using System.Web.Http.Cors;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin.Models;
using VerticalTec.POS.Service.DataSync.Owin.Services;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync.Owin
{
    public class Startup
    {
        IUnityContainer _container;

        public Startup(string dbServer, string dbName, string hangfireConnStr = "")
        {
            GlobalVar.Instance.DbServer = dbServer;
            GlobalVar.Instance.DbName = dbName;
            if (!string.IsNullOrEmpty(hangfireConnStr))
                GlobalVar.Instance.HangfireConnStr = hangfireConnStr;
        }

        private IEnumerable<IDisposable> GetHangfireServers()
        {
            GlobalConfiguration.Configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseUnityActivator(_container)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseLiteDbStorage(GlobalVar.Instance.HangfireConnStr);

            yield return new BackgroundJobServer();
        }

        public void Configuration(IAppBuilder appBuilder)
        {
            HttpConfiguration config = new HttpConfiguration();
            var cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

            _container = new UnityContainer();
            _container.RegisterType<IDatabase, MySqlDatabase>(
                new TransientLifetimeManager(),
                new InjectionConstructor(GlobalVar.Instance.DbServer, GlobalVar.Instance.DbName, "3308"));
            _container.RegisterInstance(new POSModule());
            _container.RegisterType<IDataSyncService, DataSyncService>(new TransientLifetimeManager());
            _container.RegisterType<IFailureDataSyncRecovery, FailureDataSyncRecovery>(new TransientLifetimeManager());

            config.DependencyResolver = new UnityResolver(_container);

            config.EnableSwagger(c => c.SingleApiVersion("v1", "Vtec DataSync Api Interface")).EnableSwaggerUi();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Filters.Add(new GlobalExceptionHandler());
            config.MapHttpAttributeRoutes();

            if (!string.IsNullOrEmpty(GlobalVar.Instance.HangfireConnStr))
            {
                appBuilder.UseHangfireAspNet(GetHangfireServers);
                appBuilder.UseHangfireDashboard("/jobs");

                RecurringJob.AddOrUpdate<IFailureDataSyncRecovery>(s => s.RecoveryInventoryDataSync(), Cron.Daily(3));
            }

            appBuilder.UseWebApi(config);
        }
    }
}
