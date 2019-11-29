using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VerticalTec.POS.Report.Dashboard.Models
{
    public class BillReportTree
    {
        public object Id { get; set; }
        public object ParentId { get; set; }
        public string Description { get; set; }
        public string TotalQty { get; set; }
        public string TotalAmount { get; set; }
        public bool HasItem { get; set; }
    }
}
