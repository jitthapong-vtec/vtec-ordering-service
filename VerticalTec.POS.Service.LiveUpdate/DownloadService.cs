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

        public async Task<IDownloadProgress> DownloadFile(string fileId, string savePath)
        {
            var request = _driveService.Files.Get(fileId);
            savePath += request.Execute().Name;
            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
            {
                return await request.DownloadAsync(fileStream);
            }
        }
    }
}
