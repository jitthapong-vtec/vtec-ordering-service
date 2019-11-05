using Microsoft.Owin.Hosting;
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
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.Ordering
{
    public partial class VtecOrderingService : ServiceBase
    {
        IDisposable _server;

        public VtecOrderingService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            var dbServer = ServiceConfig.GetDatabaseServer();
            var dbName = ServiceConfig.GetDatabaseName();
            var port = ServiceConfig.GetListenerPort();
            var enableLog = ServiceConfig.EnableLog();
            string baseAddress = $"http://+:{port}/";
            _server = WebApp.Start(baseAddress, appBuilder => new Startup(dbServer, dbName, enableLog).Configuration(appBuilder));
        }

        protected override void OnStop()
        {
            _server?.Dispose();
        }
    }
}
