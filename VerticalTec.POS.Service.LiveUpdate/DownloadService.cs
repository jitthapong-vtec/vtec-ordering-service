using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class DownloadService : IDownloadService
    {
        WebClient _webClient;

        public string FileName { get; private set; }

        public DownloadService()
        {
            _webClient = new WebClient();
        }

        public void DownloadFile(string filePath, string savePath)
        {
            var uri = new UriBuilder(filePath).Uri;
            var fileName = Path.GetFileName(uri.LocalPath);

            savePath += fileName;
            FileName = fileName;

            _webClient.DownloadFile(uri, savePath);
        }
    }
}
