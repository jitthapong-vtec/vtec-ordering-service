using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Share.LiveUpdate
{
    public class VersionInfo
    {
        public string BatchId { get; set; }
        public int BrandId { get; set; }
        public int ShopId { get; set; }
        public int ComputerId { get; set; }
        public int ProgramId { get; set; }
        public string ProgramName { get; set; }
        public string ProgramVersion { get; set; }
        public int BatchStatus { get; set; }
        public int VersionStatus { get; set; }
        public DateTime ScheduleUpdate { get; set; }
    }
}
