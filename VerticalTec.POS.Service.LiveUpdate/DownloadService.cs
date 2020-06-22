using Google.Apis.Download;
using Google.Apis.Drive.v3;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class DownloadService
    {
        WebClient _webClient;
        DriveService _driveService;

        public DownloadService(string apiKey)
        {
            _webClient = new WebClient();

            _driveService = new DriveService(new Google.Apis.Services.BaseClientService.Initializer()
            {
                ApiKey = apiKey
            });
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

        public async Task<DownloadInfo> DownloadFromGoogleDrive(string fileId, string savePath)
        {
            var request = _driveService.Files.Get(fileId);
            var fileName = request.Execute().Name;
            savePath += fileName;
            var downloadInfo = new DownloadInfo();
            downloadInfo.FileName = fileName;
            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
            {
                var result = await request.DownloadAsync(fileStream);
                downloadInfo.Success = result.Status == DownloadStatus.Completed;
            }
            return downloadInfo;
        }
    }

    public class DownloadInfo
    {
        public bool Success { get; set; }

        public string FileName { get; set; }
    }
}
