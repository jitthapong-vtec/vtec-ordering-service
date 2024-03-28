using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS
{
    public class VtecPOSEnv
    {
        string _softwareRootPath;
        string _frontCashierPath;
        string _patchDownloadPath;
        string _backupPath;

        public string SoftwareRootPath
        {
            get => _softwareRootPath;
            set => _softwareRootPath = value;
        }

        public string FrontCashierPath
        {
            get => _frontCashierPath;
            set => _frontCashierPath = value;
        }

        public string PatchDownloadPath
        {
            get => _patchDownloadPath;
            set => _patchDownloadPath = value;
        }

        public string BackupPath
        {
            get => _backupPath;
            set => _backupPath = value;
        }
    }
}
