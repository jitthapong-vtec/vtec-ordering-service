using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    public class VersionInfo
    {
        bool _isOnline;
        public int ShopId { get; set; }
        public int ComputerId { get; set; }
        public string ConnectionId { get; set; } = "";
        public ProgramTypes ProgramId { get; set; }
        public string ProgramName { get; set; } = "";
        public string ShopCode { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string ComputerName { get; set; } = "";
        public string ProgramVersion { get; set; } = "";
        public int VersionStatus { get; set; }
        public DateTime InsertDate { get; set; } = DateTime.MinValue;
        public DateTime UpdateDate { get; set; } = DateTime.MinValue;
        public int SyncStatus { get; set; }
        public string ProcessMessage { get; set; }
        public bool CanExecute { get; set; }
        public bool IsOnline {
            get => _isOnline;
            set
            {
                _isOnline = value;
                CanExecute = _isOnline;
            }
        }
    }
}
