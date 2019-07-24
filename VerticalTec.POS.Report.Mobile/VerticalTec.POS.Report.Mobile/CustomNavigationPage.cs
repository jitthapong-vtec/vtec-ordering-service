using Xamarin.Forms;

namespace VerticalTec.POS.Report.Mobile
{
    public class CustomNavigationPage : NavigationPage
    {
        public CustomNavigationPage(Page root) : base(root)
        {
            Popped += CustomNavigationPage_Popped;
        }

        private void CustomNavigationPage_Popped(object sender, NavigationEventArgs e)
        {
        }

        protected override bool OnBackButtonPressed()
        {
            return base.OnBackButtonPressed();
        }
    }
}
