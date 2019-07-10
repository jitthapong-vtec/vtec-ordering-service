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
using System.Web.Http.Services;
using VerticalTec.POS.Service.DataSync.Owin;

namespace VerticalTec.POS.Service.DataSync
{
    public partial class VtecDataSyncService : ServiceBase
    {
        IDisposable _server;

        public VtecDataSyncService()
        {
            InitializeComponent();
            var logPath = $"{Path.GetDirectoryName(Config.GetExecPath())}/Log/";
            LogManager.Instance.InitLogManager(logPath, "vt_sync_service_");
        }

        protected override void OnStart(string[] args)
        {
            string baseAddress = "http://+:9000/";
            var dbServer = Config.GetDatabaseServer();
            var dbName = Config.GetDatabaseName();
            _server = WebApp.Start(baseAddress, appBuilder => new Startup(dbServer, dbName).Configuration(appBuilder));
            LogManager.Instance.WriteLog("Start owin api");
        }

        protected override void OnStop()
        {
            _server?.Dispose();
            LogManager.Instance.WriteLog("Service already stop");
        }
    }
}
