using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdateClient
{
    public class DownloadService
    {
        DriveService _driveService;

        public DownloadService(IConfiguration config)
        {
            var driveApiKey = config.GetValue<string>("DriveApiKey");
            _driveService = new DriveService(new Google.Apis.Services.BaseClientService.Initializer()
            {
                ApiKey = driveApiKey
            });
        }

        public async Task<IDownloadProgress> DownloadFile(string fileId, string savePath)
        {
            var request = _driveService.Files.Get(fileId);
            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
            {
                return await request.DownloadAsync(fileStream);
            }
        }
    }
}
