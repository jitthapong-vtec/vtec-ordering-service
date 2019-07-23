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
    public partial class MainPage : ContentPage, IPage
    {
        public MainPage()
        {
            InitializeComponent();
            LoadUrl();
        }

        public void NavigationPopped()
        {
            LoadUrl();
        }

        void LoadUrl()
        {
            var url = Preferences.Get("ReportUrl", "");
            webView.Source = url;
        }
    }
}
