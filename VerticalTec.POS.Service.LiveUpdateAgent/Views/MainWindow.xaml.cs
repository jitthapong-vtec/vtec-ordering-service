using System.Windows;
using VerticalTec.POS.Service.LiveUpdateAgent.ViewModels;

namespace VerticalTec.POS.Service.LiveUpdateAgent.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = (DataContext as MainWindowViewModel).OnUpdating;
        }
    }
}
