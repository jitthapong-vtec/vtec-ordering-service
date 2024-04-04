using Microsoft.Owin.Hosting;
using OrderingService.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using Form = System.Windows.Forms;

namespace OrderingService
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IDisposable _host;

        private Form.NotifyIcon _notifyIcon;
        private Form.ToolStripMenuItem _menuSetting;
        private Form.ToolStripMenuItem _menuExit;

        public App()
        {
            InitToolStripMenu();
        }

        private void InitToolStripMenu()
        {
            _notifyIcon = new Form.NotifyIcon()
            {
                ContextMenuStrip = new Form.ContextMenuStrip(),
                Text = "vTec Ordering Service",
                Visible = true
            };

            _notifyIcon.Click += _notifyIcon_Click;

            try
            {
                var res = GetResourceStream(new Uri("pack://application:,,,/OrderingService;component/Resources/Icon/Disk.ico"));
                _notifyIcon.Icon = new System.Drawing.Icon(res.Stream);
                _notifyIcon.BalloonTipIcon = Form.ToolTipIcon.Info;
            }
            catch { }

            System.Drawing.Image settingImg = null;
            try
            {
                var res = GetResourceStream(new Uri("pack://application:,,,/OrderingService;component/Resources/Icon/Settings-36.png"));
                settingImg = new System.Drawing.Bitmap(res.Stream);
            }
            catch { }

            System.Drawing.Image exitImg = null;
            try
            {
                var res = GetResourceStream(new Uri("pack://application:,,,/OrderingService;component/Resources/Icon/Shutdown-40.png"));
                exitImg = new System.Drawing.Bitmap(res.Stream);
            }
            catch { }

            _menuSetting = new Form.ToolStripMenuItem("Settings", settingImg, ShowSettingWindow, "SettingMenu");
            _menuExit = new Form.ToolStripMenuItem("Exit", exitImg, ExitApp, "ExitMenu");

            var menus = new[] { _menuSetting, _menuExit };
            _notifyIcon.ContextMenuStrip.Items.AddRange(menus);
        }

        private void ExitApp(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Do you want to exit?", "Exit", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                Shutdown();
            }
        }

        private void ShowSettingWindow(object sender, EventArgs e)
        {
            var settingWindow = new SettingWindow();
            var result = settingWindow.ShowDialog();
            if (result == true)
            {
                RunOrderingApi();
            }
        }

        private void _notifyIcon_Click(object sender, EventArgs e)
        {
            var args = e as Form.MouseEventArgs;
            if (args?.Button == Form.MouseButtons.Left)
            {
                MethodInfo mi = typeof(Form.NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                mi?.Invoke(_notifyIcon, null);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RunOrderingApi();

            _notifyIcon.ShowBalloonTip(5000, "vTec Ordering Service", "vTec ordering service is running", Form.ToolTipIcon.Info);
        }

        private void RunOrderingApi()
        {
            var dbServer = Settings.Default.DBServer;
            var dbName = Settings.Default.DBName;
            var apiPort = Settings.Default.APIPort;
            var rcAgentPath = Settings.Default.RCAgentPath;

            var baseAddress = $"http://127.0.0.1:{apiPort}";
            
            if (IsAdministrator)
                baseAddress = $"http://+:{apiPort}/";

            try
            {
                var hangfireConStr = Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).AbsolutePath)) + "\\hangfire.db";

                _host?.Dispose();
                _host = WebApp.Start(baseAddress,
                    appBuilder => new VerticalTec.POS.Service.Ordering.Owin.Startup(dbServer, dbName, hangfireConStr, rcAgentPath).Configuration(appBuilder));
            }
            catch (Exception ex)
            {
            }
        }

        public bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
