using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.DataSync.Owin.Models
{
    public class ExportInvenData
    {
        public string BatchUuid { get; set; }
        public int ShopId { get; set; }
        public int ExportType { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string Json { get; set; }
    }
}
