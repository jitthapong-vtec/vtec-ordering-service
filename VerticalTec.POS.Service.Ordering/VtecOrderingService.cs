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
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.Ordering
{
    public partial class VtecOrderingService : ServiceBase
    {
        const string LogPrefix = "Service_";

        public VtecOrderingService()
        {
            InitializeComponent();
            var logPath = $"{Path.GetDirectoryName(Config.GetExecPath())}/Log/";
            LogManager.Instance.InitLogManager(logPath);
            var enableLog = Config.IsEnableLog();
            LogManager.Instance.EnableLog = enableLog;
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
