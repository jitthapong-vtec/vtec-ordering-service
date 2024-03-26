using Microsoft.Owin.Hosting;
using OrderingService.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
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
                var resourceStream = GetResourceStream(new Uri("pack://application:,,,/OrderingService;component/Resources/Icon/orderingservice.ico"));
                _notifyIcon.Icon = new System.Drawing.Icon(resourceStream.Stream);
            }
            catch { }

            _menuSetting = new Form.ToolStripMenuItem("Settings", null, ShowSettingWindow, "SettingMenu");
            _menuExit = new Form.ToolStripMenuItem("Exit", null, ExitApp, "ExitMenu");

            var menus = new[] {
                _menuSetting,
                _menuExit
            };
            _notifyIcon.ContextMenuStrip.Items.AddRange(menus);
        }

        private void ExitApp(object sender, EventArgs e)
        {
            Shutdown();
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
        }

        private void RunOrderingApi()
        {
            var dbServer = Settings.Default.DBServer;
            var dbName = Settings.Default.DBName;
            var apiPort = Settings.Default.APIPort;
            var rcAgentPath = Settings.Default.RCAgentPath;

            //var baseAddress = $"http://+:{apiPort}/";
            var baseAddress = $"http://127.0.0.1:{apiPort}";
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

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
