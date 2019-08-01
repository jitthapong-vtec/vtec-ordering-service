using System.ComponentModel;
using Xamarin.Forms;

namespace VerticalTec.POS.Report.Mobile.Views
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        int _totalBackClick;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override bool OnBackButtonPressed()
        {
            return true;
        }

        public void NavigationPopped()
        {
        }

        private void WebView_Navigating(object sender, WebNavigatingEventArgs e)
        {

        }

        private void WebView_Navigated(object sender, WebNavigatedEventArgs e)
        {

        }
    }
}
