using System.Threading.Tasks;
using VerticalTec.POS.Report.Mobile.Controls;
using VerticalTec.POS.Report.Mobile.Views;
using Xamarin.Forms;

namespace VerticalTec.POS.Report.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            Page page = new MainPage();
            MainPage = new CustomNavigationPage(page);
        }

        protected override void OnStart()
        {
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
