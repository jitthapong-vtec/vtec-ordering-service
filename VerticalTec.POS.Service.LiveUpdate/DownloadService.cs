using Google.Apis.Download;
using Google.Apis.Drive.v3;
using System.IO;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class DownloadService
    {
        DriveService _driveService;

        public DownloadService(string apiKey)
        {
            _driveService = new DriveService(new Google.Apis.Services.BaseClientService.Initializer()
            {
                ApiKey = apiKey
            });
        }

        public async Task<DownloadInfo> DownloadFile(string fileId, string savePath)
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
