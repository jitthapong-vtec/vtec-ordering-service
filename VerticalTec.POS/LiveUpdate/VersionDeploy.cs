using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    public class VersionDeploy
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public int BrandId { get; set; }
        public int ShopId { get; set; }
        public int ProgramId { get; set; }
        public string ProgramName { get; set; } = "";
        public string ProgramVersion { get; set; } = "";
        public string FileId { get; set; } = "";
        public int BatchStatus { get; set; }
        public DateTime ScheduleUpdate { get; set; } = DateTime.MinValue;
        public DateTime InsertDate { get; set; } = DateTime.MinValue;
        public DateTime UpdateDate { get; set; } = DateTime.MinValue;
    }
}
