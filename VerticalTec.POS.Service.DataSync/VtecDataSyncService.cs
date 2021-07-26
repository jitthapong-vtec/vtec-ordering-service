using Microsoft.Owin.Hosting;
using System;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin;
using VerticalTec.POS.Service.DataSync.Owin.Services;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync
{
    public partial class VtecDataSyncService : ServiceBase
    {
        const string LogPrefix = "Service_";

        private Timer _timer;
        private DateTime _timeToSyncInv = DateTime.MinValue;
        private bool _isSyncing;

        private string _dbServer;
        private string _dbName;

        IDisposable _server;

        public VtecDataSyncService()
        {
            InitializeComponent();
            var logPath = $"{Path.GetDirectoryName(Config.GetExecPath())}/Log/";
            LogManager.Instance.InitLogManager(logPath);
            var enableLog = Config.IsEnableLog();
            LogManager.Instance.EnableLog = enableLog;
        }

        protected override void OnStart(string[] args)
        {
            _timeToSyncInv = Config.TimeToSyncInven();
            _dbServer = Config.GetDatabaseServer();
            _dbName = Config.GetDatabaseName();
            var port = Config.GetPort();

            string baseAddress = $"http://+:{port}/";
            var hangfireConStr = Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).AbsolutePath)) + "\\hangfire.db";

            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;

            if (_timeToSyncInv > DateTime.MinValue)
                _timer.Start();

            _server = WebApp.Start(baseAddress, appBuilder => new Startup(_dbServer, _dbName, hangfireConStr).Configuration(appBuilder));
            LogManager.Instance.WriteLog("Start owin api", LogPrefix);
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_isSyncing)
                return;

            var now = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);

            if (TimeSpan.Compare(now.TimeOfDay, _timeToSyncInv.TimeOfDay) == 0)
            {
                LogManager.Instance.WriteLog($"Schedule begin {_timeToSyncInv}");
                try
                {
                    _isSyncing = true;
                    var db = new MySqlDatabase(_dbServer, _dbName, "3308");
                    using (var conn = db.Connect())
                    {
                        var posModule = new POSModule();
                        var syncService = new DataSyncService(db, posModule);
                        syncService.SyncInvenData(conn, 0).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteLog($"schedule error {ex.Message}");
                }
            }
            else
            {
                if (_isSyncing)
                    _isSyncing = false;
            }
        }

        protected override void OnStop()
        {
            if (_timer != null)
                _timer.Elapsed -= _timer_Elapsed;

            _server?.Dispose();
            LogManager.Instance.WriteLog("Service already stop", LogPrefix);
        }
    }
}
