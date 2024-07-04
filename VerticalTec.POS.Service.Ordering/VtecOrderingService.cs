using Microsoft.Owin.Hosting;
using Microsoft.TeamFoundation.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Service.Ordering.Owin;
using VerticalTec.POS.Service.Ordering.ThirdpartyInterface;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.Ordering
{
    public partial class VtecOrderingService : ServiceBase
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        IDisposable _server;
        OrderServiceWorker _orderServiceWorker;

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

            try
            {
                _orderServiceWorker = new OrderServiceWorker(dbServer, dbName);
                Task.Run(() => _orderServiceWorker.InitConnectionAsync());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "OrderServiceWorker InitConnectionAsync");
            }

        }

        protected override void OnStop()
        {
            _server?.Dispose();
            _orderServiceWorker?.Dispose();
        }
    }
}
