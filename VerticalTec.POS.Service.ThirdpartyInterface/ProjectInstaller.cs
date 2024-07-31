using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.ThirdpartyInterface
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
            AfterInstall += ProjectInstaller_AfterInstall;
        }

        private void ProjectInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            StartService();
        }
        private void StartService()
        {
            using (ServiceController sc = new ServiceController(serviceInstaller1.ServiceName))
            {
                sc.Start();
            }
        }

        public override void Install(IDictionary stateSaver)
        {
            string dbServer = Context.Parameters["DBServer"];
            string dbName = Context.Parameters["DBName"];
            string serviceUrl = Context.Parameters["OrderingServiceUrl"];

            var execPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var config = ConfigurationManager.OpenExeConfiguration(execPath);
            config.AppSettings.Settings["DBServer"].Value = dbServer;
            config.AppSettings.Settings["DBName"].Value = dbName;
            config.AppSettings.Settings["OrderingServiceUrl"].Value = serviceUrl;
            config.Save();

            base.Install(stateSaver);
        }
    }
}
