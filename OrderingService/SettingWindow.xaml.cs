using OrderingService.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OrderingService
{
    /// <summary>
    /// Interaction logic for SettingWindow.xaml
    /// </summary>
    public partial class SettingWindow : Window
    {
        public SettingWindow()
        {
            InitializeComponent();

            txtDbServer.Text = Settings.Default.DBServer;
            txtDbName.Text = Settings.Default.DBName;
            txtApiPort.Text = Settings.Default.APIPort;
            txtRCAgentPath.Text = Settings.Default.RCAgentPath;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.DBServer = txtDbServer.Text;
            Settings.Default.DBName = txtDbName.Text;
            Settings.Default.APIPort = txtApiPort.Text;
            Settings.Default.Save();

            DialogResult = true;
            Close();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.ShowDialog();
                txtRCAgentPath.Text = dialog.SelectedPath;
            }
        }
    }
}
