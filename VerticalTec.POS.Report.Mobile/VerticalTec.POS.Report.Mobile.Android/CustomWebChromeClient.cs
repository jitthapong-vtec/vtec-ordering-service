using Android.Util;
using Android.Webkit;
using System;

namespace VerticalTec.POS.Report.Mobile.Droid
{
    public class CustomWebChromeClient : WebChromeClient
    {
        public event EventHandler<int> ProgressEvent;

        public override bool OnConsoleMessage(ConsoleMessage cm)
        {
            Log.Debug("MyApplication", cm.Message() + " -- From line "
                         + cm.LineNumber() + " of "
                         + cm.SourceId());
            return true;
        }

        public override void OnProgressChanged(WebView view, int newProgress)
        {
            base.OnProgressChanged(view, newProgress);
            ProgressEvent?.Invoke(this, newProgress);
        }
    }
}