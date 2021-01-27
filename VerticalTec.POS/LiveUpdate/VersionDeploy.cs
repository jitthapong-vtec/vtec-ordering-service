using System;
using System.ComponentModel.DataAnnotations;

namespace VerticalTec.POS.LiveUpdate
{
    public class VersionDeploy
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public int BrandId { get; set; }
        public ProgramTypes ProgramId { get; set; }
        public string ProgramName { get; set; } = "";
        [Required(ErrorMessage = "Version is required")]
        public string ProgramVersion { get; set; } = "";
        [Required(ErrorMessage = "Please upload file")]
        public string FileUrl { get; set; } = "";
        public VersionDeployBatchStatus BatchStatus { get; set; }
        public bool AutoBackup { get; set; }
        public int CreateBy { get; set; }
        public int UpdateBy { get; set; }
        public string CreateName { get; set; } = "";
        public string UpdateName { get; set; } = "";
        public DateTime ScheduleUpdate { get; set; } = DateTime.MinValue;
        public DateTime InsertDate { get; set; } = DateTime.MinValue;
        public DateTime UpdateDate { get; set; } = DateTime.MinValue;
    }
}
