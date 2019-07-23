using System;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace VerticalTec.POS.Report.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            Page page = new MainPage();
            if (string.IsNullOrEmpty(Preferences.Get("ReportUrl", "")))
                page = new SettingPage();
            MainPage = new CustomNavigationPage(page);
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
