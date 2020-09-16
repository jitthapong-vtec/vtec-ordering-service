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

        public bool IsCanceled { get; private set; }
        public bool IsComplete { get; private set; }
        public string FileName { get; private set; }

        public DownloadService()
        {
            _webClient = new WebClient();
            _webClient.DownloadFileCompleted += _webClient_DownloadFileCompleted;
        }

        private void _webClient_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
                IsCanceled = true;
            IsComplete = true;
        }

        public void DownloadFile(string filePath, string savePath)
        {
            IsComplete = false;
            IsCanceled = false;

            var uri = new UriBuilder(filePath).Uri;
            var fileName = Path.GetFileName(uri.LocalPath);

            savePath += fileName;
            FileName = fileName;
            _webClient.DownloadFileAsync(uri, savePath);

            while (!IsComplete) ;
        }

        public void Cancel()
        {
            _webClient.CancelAsync();
        }
    }
}
