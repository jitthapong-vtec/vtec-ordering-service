﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    public class VersionLiveUpdate
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public int BranId { get; set; }
        public int ShopId { get; set; }
        public int ComputerId { get; set; }
        public int ProgramId { get; set; }
        public string ProgramName { get; set; } = "";
        public string UpdateVersion { get; set; }
        public int RevFile { get; set; }
        public DateTime RevStartTime { get; set; } = DateTime.MinValue;
        public DateTime RevEndTime { get; set; } = DateTime.MinValue;
        public int BackupStatus { get; set; }
        public DateTime BackupStartTime { get; set; } = DateTime.MinValue;
        public DateTime BackupEndTime { get; set; } = DateTime.MinValue;
        public DateTime ScheduleUpdate { get; set; } = DateTime.MinValue;
        public DateTime UpdateStartTime { get; set; } = DateTime.MinValue;
        public DateTime UpdateEndTime { get; set; } = DateTime.MinValue;
        public int RollbackStatus { get; set; }
        public int UpdateStatus { get; set; }
        public int SyncStatus { get; set; }
        [MaxLength(2000)]
        public string MessageLog { get; set; } = "";
        public DateTime InsertDate { get; set; } = DateTime.MinValue;
        public DateTime UpdateDate { get; set; } = DateTime.MinValue;
    }
}
