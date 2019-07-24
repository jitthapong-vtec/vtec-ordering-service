using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace VerticalTec.POS.Report.Mobile
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
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
