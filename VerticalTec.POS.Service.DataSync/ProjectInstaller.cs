using Microsoft.TeamFoundation.Common;
using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;
using System.ServiceProcess;

namespace VerticalTec.POS.Service.DataSync
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
            AfterInstall += new InstallEventHandler(Service_AfterInstall);
        }

        private void Service_AfterInstall(object sender, InstallEventArgs e)
        {
            StartService();
            SetFirewallRule();
        }

        void SetFirewallRule()
        {
            INetFwMgr icfMgr = null;
            try
            {
                Type TicfMgr = Type.GetTypeFromProgID("HNetCfg.FwMgr");
                icfMgr = (INetFwMgr)Activator.CreateInstance(TicfMgr);
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                INetFwProfile profile;
                INetFwOpenPort portClass;
                Type TportClass = Type.GetTypeFromProgID("HNetCfg.FWOpenPort");
                portClass = (INetFwOpenPort)Activator.CreateInstance(TportClass);

                // Get the current profile
                profile = icfMgr.LocalPolicy.CurrentProfile;

                var port = Config.GetPort();
                // Set the port properties
                portClass.Scope = NET_FW_SCOPE_.NET_FW_SCOPE_ALL;
                portClass.Enabled = true;
                portClass.Protocol = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
                portClass.Name = "Vtec Data Synchronization Service";
                portClass.Port = Convert.ToInt32(port);

                // Add the port to the ICF Permissions List
                profile.GloballyOpenPorts.Add(portClass);
                return;
            }
            catch (Exception)
            {
            }
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
            string port = Context.Parameters["Port"];

            var execPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var config = ConfigurationManager.OpenExeConfiguration(execPath);
            config.AppSettings.Settings["DBServer"].Value = dbServer;
            config.AppSettings.Settings["DBName"].Value = dbName;
            config.AppSettings.Settings["Port"].Value = port;
            config.Save();

            base.Install(stateSaver);
        }
    }
}
