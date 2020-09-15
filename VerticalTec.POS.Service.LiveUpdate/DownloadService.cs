using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class DownloadService
    {
        WebClient _webClient;

        public DownloadService()
        {
            _webClient = new WebClient();
        }

        public DownloadInfo DownloadFile(string filePath, string savePath)
        {
            var uri = new UriBuilder(filePath).Uri;
            var fileName = Path.GetFileName(uri.LocalPath);

            savePath += fileName;
            var downloadInfo = new DownloadInfo();
            downloadInfo.FileName = fileName;
            _webClient.DownloadFile(uri, savePath);
            downloadInfo.Success = true;
            return downloadInfo;
        }
    }

    public class DownloadInfo
    {
        public bool Success { get; set; }

        public string FileName { get; set; }
    }
}
