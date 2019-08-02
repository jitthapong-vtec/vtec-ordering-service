using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;

namespace VerticalTec.POS.Report.Mobile.Controls
{
    public class CustomWebView : View
    {
        public event EventHandler WebSuccess;

        public static readonly BindableProperty HtmlFileNameProperty = BindableProperty.Create(
            "HtmlFileName", typeof(string), typeof(CustomWebView), default(string));

        public static readonly BindableProperty UrlProperty = BindableProperty.Create(
            "Url", typeof(string), typeof(CustomWebView), default(string));

        public static readonly BindableProperty JsInterfaceOnWebSuccessProperty = BindableProperty.Create(
            "JsInterfaceOnWebSuccess", typeof(bool), typeof(CustomWebView), false, propertyChanged: OnWebSuccessPropertyChanged);

        public static readonly BindableProperty LoadingProperty = BindableProperty.Create(
            "Loading", typeof(bool), typeof(CustomWebView), false, BindingMode.TwoWay);

        private static void OnWebSuccessPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var ctrl = ((CustomWebView)bindable);
            ctrl.WebSuccess?.Invoke(ctrl, new EventArgs());
        }

        public string HtmlFileName
        {
            get => (string)GetValue(HtmlFileNameProperty);
            set => SetValue(HtmlFileNameProperty, value);
        }

        public string Url
        {
            get => (string)GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        public bool JsInterfaceOnWebSuccess
        {
            get => (bool)GetValue(JsInterfaceOnWebSuccessProperty);
            set => SetValue(JsInterfaceOnWebSuccessProperty, value);
        }

        public bool Loading
        {
            get => (bool)GetValue(LoadingProperty);
            set => SetValue(LoadingProperty, value);
        }
    }
}
