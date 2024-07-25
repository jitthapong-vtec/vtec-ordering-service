using Microsoft.Owin.Hosting;
using System;
using System.IO;
using System.ServiceProcess;
using VerticalTec.POS.Service.Ordering.Owin;

namespace VerticalTec.POS.Service.Ordering
{
    public partial class VtecOrderingService : ServiceBase
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        IDisposable _server;

        public VtecOrderingService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            var dbServer = ServiceConfig.GetDatabaseServer();
            var dbName = ServiceConfig.GetDatabaseName();
            var apiPort = ServiceConfig.GetApiPort();
            var apiUser = ServiceConfig.GetApiUser();
            var apiPassword = ServiceConfig.GetApiPassword();
            string baseAddress = $"http://+:{apiPort}/";

            _logger.Info("Start OrderingService Api");

            var hangfireConStr = Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).AbsolutePath)) + "\\hangfire.db";
            _server = WebApp.Start(baseAddress, appBuilder => new Startup(dbServer, dbName, hangfireConStr, apiUser: apiUser, apiPass: apiPassword).Configuration(appBuilder));
        }

        protected override void OnStop()
        {
            _server?.Dispose();
        }
    }
}
