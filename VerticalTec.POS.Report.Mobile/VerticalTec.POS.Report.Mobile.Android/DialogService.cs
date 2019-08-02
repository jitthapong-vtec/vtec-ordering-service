using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Plugin.CurrentActivity;
using VerticalTec.POS.Report.Mobile.Droid;
using VerticalTec.POS.Report.Mobile.Services;
using Xamarin.Forms;

[assembly: Dependency(typeof(DialogService))]
namespace VerticalTec.POS.Report.Mobile.Droid
{
    public class DialogService : IDialogService
    {
        public void ShowToast(string message)
        {
            Toast.MakeText(CrossCurrentActivity.Current.AppContext, message, ToastLength.Short).Show();
        }
    }
}