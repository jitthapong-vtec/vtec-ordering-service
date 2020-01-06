using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    public class VersionInfo
    {
        public int ShopId { get; set; }
        public int ComputerId { get; set; }
        public int ProgramId { get; set; }
        public string ProgramName { get; set; } = "";
        public string ProgramVersion { get; set; } = "";
        public int VersionStatus { get; set; }
        public DateTime InsertDate { get; set; } = DateTime.MinValue;
        public DateTime UpdateDate { get; set; } = DateTime.MinValue;
        public int SyncStatus { get; set; }
    }
}
