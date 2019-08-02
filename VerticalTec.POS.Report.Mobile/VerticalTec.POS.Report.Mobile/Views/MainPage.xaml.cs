using System;
using System.ComponentModel;
using VerticalTec.POS.Report.Mobile.Services;
using VerticalTec.POS.Report.Mobile.ViewModels;
using Xamarin.Forms;

namespace VerticalTec.POS.Report.Mobile.Views
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        int _totalBackClick;

        MainViewModel _viewModel;

        public MainPage()
        {
            InitializeComponent();
            _viewModel = BindingContext as MainViewModel;
            Appearing += async (s, e) => await _viewModel.LoadUrl();
        }

        protected override bool OnBackButtonPressed()
        {
            if (++_totalBackClick == 1)
            {
                Device.StartTimer(TimeSpan.FromSeconds(1), () =>
                {
                    _totalBackClick = 0;
                    return false;
                });
                DependencyService.Get<IDialogService>().ShowToast("Press back again to exit");
                return true;
            }
            return base.OnBackButtonPressed();
        }

        public void NavigationPopped()
        {
        }

        private void WebView_Navigating(object sender, WebNavigatingEventArgs e)
        {
            _viewModel.IsBusy = true;
        }

        private void WebView_Navigated(object sender, WebNavigatedEventArgs e)
        {
            _viewModel.IsBusy = false;
        }
    }
}
