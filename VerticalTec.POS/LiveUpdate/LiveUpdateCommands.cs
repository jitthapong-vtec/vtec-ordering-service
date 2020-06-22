using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    [Flags]
    public enum LiveUpdateCommands
    {
        ReceiveVersionDeploy,
        SendVersionInfo,
        DownloadFile,
        BackupFile
    }
}
