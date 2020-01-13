using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    public class VersionDeploy
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString().ToUpper();
        public int BrandId { get; set; }
        public int ShopId { get; set; }
        public ProgramTypes ProgramId { get; set; } = ProgramTypes.FrontCashier;
        public string ProgramName { get; set; } = "";
        public string ProgramVersion { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int BatchStatus { get; set; }
        public DateTime ScheduleUpdate { get; set; } = DateTime.MinValue;
        public DateTime InsertDate { get; set; } = DateTime.MinValue;
        public DateTime UpdateDate { get; set; } = DateTime.MinValue;
    }
}
