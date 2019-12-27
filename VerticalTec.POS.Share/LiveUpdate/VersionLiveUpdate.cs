using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace VerticalTec.POS.Share.LiveUpdate
{
    public class VersionLiveUpdate : VersionInfo
    {
        public int RevFile { get; set; }
        public DateTime RevStartTime { get; set; } = DateTime.MinValue;
        public DateTime RevEndTime { get; set; } = DateTime.MinValue;
        public int BackupStatus { get; set; }
        public int UpdateStatus { get; set; }
        public DateTime BackupStartTime { get; set; } = DateTime.MinValue;
        public DateTime BackupEndTime { get; set; } = DateTime.MinValue;
        public DateTime ScheduleUpdate { get; set; } = DateTime.MinValue;
        public DateTime UpdateStartTime { get; set; } = DateTime.MinValue;
        public DateTime UpdateEndTime { get; set; } = DateTime.MinValue;
        public int RollbackStatus { get; set; }
        [MaxLength(200)]
        public string MessageLog { get; set; }
    }
}
