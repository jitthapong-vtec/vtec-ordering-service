using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class KDSClient
    {
        public int KDSId { get; set; }
        public int ComputerId { get; set; }
        public string ComputerName { get; set; }
        public string ConnectionId { get; set; }
    }
}
