using Android.Content;
using System.ComponentModel;
using VerticalTec.POS.Report.Mobile.Controls;
using VerticalTec.POS.Report.Mobile.Droid;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

[assembly: ExportRenderer(typeof(CustomWebView), typeof(CustomWebViewRenderer))]
namespace VerticalTec.POS.Report.Mobile.Droid
{
    public class CustomWebViewRenderer : ViewRenderer<CustomWebView, Android.Webkit.WebView>
    {
        public CustomWebViewRenderer(Context context) : base(context)
        {
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CustomWebView> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement != null)
            {
                if (Control == null)
                {
                    var webChromeClient = new CustomWebChromeClient();
                    webChromeClient.ProgressEvent += OnLoadingProgress;

                    var webView = new Android.Webkit.WebView(Context);
                    webView.SetWebChromeClient(webChromeClient);
                    webView.Settings.JavaScriptEnabled = true;
                    webView.Settings.DomStorageEnabled = true;

                    SetNativeControl(webView);

                    LoadUrl();
                }
            }
        }

        private void OnLoadingProgress(object sender, int newProgress)
        {
            if (Element == null)
                return;
            if (newProgress < 100)
                Element.Loading = true;
            else
                Element.Loading = false;
        }

        void LoadUrl()
        {
            Control.LoadUrl(Element.Url);
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);
            if (e.PropertyName == "Url")
            {
                LoadUrl();
            }
        }
    }
}