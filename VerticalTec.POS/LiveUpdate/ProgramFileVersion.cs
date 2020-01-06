using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.LiveUpdate
{
    public class ProgramFileVersion
    {
        public int ShopId { get; set; }
        public int ComputerId { get; set; }
        public string FileName { get; set; }
        public string FileVersion { get; set; }
        public DateTime FileDate { get; set; } = DateTime.MinValue;
        public DateTime LastUpdateDate { get; set; } = DateTime.MinValue;
    }
}
