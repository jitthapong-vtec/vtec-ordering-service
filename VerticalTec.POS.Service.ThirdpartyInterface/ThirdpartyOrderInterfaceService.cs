using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using VerticalTec.POS.Service.ThirdpartyInterface.Worker;

namespace VerticalTec.POS.Service.ThirdpartyInterface
{
    public partial class ThirdpartyOrderInterfaceService : ServiceBase
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private OrderServiceWorker _orderServiceWorker;

        public ThirdpartyOrderInterfaceService()
        {
            InitializeComponent();
        }

        protected override async void OnStart(string[] args)
        {
            var dbServer = ServiceConfig.GetDatabaseServer();
            var dbName = ServiceConfig.GetDatabaseName();
            var orderingServiceUrl = ServiceConfig.GetOrderingServiceUrl();

            try
            {
                _logger.Info("Starting service...");
                _orderServiceWorker?.Dispose();
                _orderServiceWorker = new OrderServiceWorker(dbServer, dbName, orderingServiceUrl);
                await _orderServiceWorker.InitConnectionAsync();
                _logger.Info("Service is running");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Start service error");
            }
        }

        protected override void OnStop()
        {
            _orderServiceWorker?.Dispose();
        }
    }
}
