using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public interface IDownloadService
    {
        bool IsCanceled { get; }
        bool IsComplete { get; }
        string FileName { get; }

        void DownloadFile(string filePath, string savePath);

        void Cancel();
    }
}
