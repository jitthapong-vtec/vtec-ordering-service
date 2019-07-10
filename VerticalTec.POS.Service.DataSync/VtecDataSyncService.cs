using Microsoft.Owin.Hosting;
using System;
using System.IO;
using System.ServiceProcess;
using VerticalTec.POS.Service.DataSync.Owin;
using VerticalTec.POS.Utils;

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
            var dbServer = Config.GetDatabaseServer();
            var dbName = Config.GetDatabaseName();
            var port = Config.GetPort();
            string baseAddress = $"http://+:{port}/";
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
