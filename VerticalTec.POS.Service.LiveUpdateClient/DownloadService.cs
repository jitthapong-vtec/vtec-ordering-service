using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;

namespace VerticalTec.POS.Service.LiveUpdateClient
{
    public class DownloadService
    {
        WebClient _client;

        Action<AsyncCompletedEventArgs> _downLoadCompleteAction;

        public DownloadService(Action<AsyncCompletedEventArgs> downloadCompleteAction)
        {
            _client = new WebClient();
            _downLoadCompleteAction = downloadCompleteAction;
            _client.DownloadFileCompleted += DownloadFileCompleted;
        }

        public void DownloadFile(Uri remoteUri, string savePath)
        {
            _client.DownloadFileAsync(remoteUri, savePath);
        }

        private void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            _downLoadCompleteAction(e);
        }
    }
}
