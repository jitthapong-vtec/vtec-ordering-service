using Prism.Events;
using System;
using System.Windows.Controls;
using VerticalTec.POS.Service.LiveUpdateAgent.Events;

namespace VerticalTec.POS.Service.LiveUpdateAgent.Views
{
    /// <summary>
    /// Interaction logic for MainView
    /// </summary>
    public partial class MainView : UserControl
    {
        IEventAggregator _eventAggregator;

        public MainView(IEventAggregator ea)
        {
            InitializeComponent();

            _eventAggregator = ea;

            Loaded += MainView_Loaded;
            Unloaded += MainView_Unloaded;
        }

        private void MainView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _eventAggregator.GetEvent<UpdateInfoMessageEvent>().Subscribe(OnInfoMessageUpdate);
        }

        private void MainView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _eventAggregator.GetEvent<UpdateInfoMessageEvent>().Unsubscribe(OnInfoMessageUpdate);
        }

        private void OnInfoMessageUpdate(string message)
        {
            scrollViewer.ScrollToEnd();
        }
    }
}
