using System;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace VerticalTec.POS.Report.Mobile
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SettingPage : ContentPage
    {
        public SettingPage()
        {
            InitializeComponent();
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            var url = txtUrl.Text;
            if (!string.IsNullOrEmpty(url))
                Preferences.Set("ReportUrl", url);

            await App.Current.MainPage.Navigation.PopAsync();
        }
    }
}