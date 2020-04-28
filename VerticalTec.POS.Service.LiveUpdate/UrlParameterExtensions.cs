using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public static class UrlParameterExtensions
    {
        public static string GetValue(this string url, string parameter)
        {
            var uri = new Uri(url);
            return HttpUtility.ParseQueryString(uri.Query).Get(parameter);
        }
    }
}
