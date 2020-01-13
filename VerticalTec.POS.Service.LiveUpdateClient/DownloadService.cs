using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;

namespace VerticalTec.POS.Service.LiveUpdateClient
{
    public class DownloadService
    {
        DriveService _driveService;
        Action<bool, string> _downloadAction;

        public DownloadService(IConfiguration config, Action<bool, string> downloadAction)
        {
            var driveApiKey = config.GetValue<string>("DriveApiKey");
            _driveService = new DriveService(new Google.Apis.Services.BaseClientService.Initializer()
            {
                ApiKey = driveApiKey
            });
            _downloadAction = downloadAction;
        }

        public void DownloadFile(string fileId, string savePath)
        {
            var request = _driveService.Files.Get(fileId);
            try
            {
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                {
                    request.MediaDownloader.ProgressChanged += (IDownloadProgress progress) =>
                    {
                        switch (progress.Status)
                        {
                            case DownloadStatus.Completed:
                                {
                                    _downloadAction(true, "Download complete");
                                    break;
                                }
                            case DownloadStatus.Failed:
                                {
                                    _downloadAction(false, $"Download failed {progress.Exception.Message}");
                                    break;
                                }
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _downloadAction(false, ex.Message);
            }
        }
    }
}
