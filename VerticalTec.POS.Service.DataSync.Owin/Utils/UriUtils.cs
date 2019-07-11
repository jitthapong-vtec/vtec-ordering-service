using System;

namespace VerticalTec.POS.Service.DataSync.Owin.Utils
{
    public class UriUtils
    {
        public static string ValidateUriFormat(string url)
        {
            var completeUrl = url;
            Uri uriResult;
            var isValidUrl = Uri.TryCreate(url, UriKind.Absolute, out uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            if (!isValidUrl)
                completeUrl = $"http://{url}";
            return completeUrl;
        }
    }
}
