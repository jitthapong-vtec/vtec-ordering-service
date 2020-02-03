using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    [Flags]
    public enum LiveUpdateCommands
    {
        SendVersionInfo,
        DownloadFile,
        BackupFile
    }
}
