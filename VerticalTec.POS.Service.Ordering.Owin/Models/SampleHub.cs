using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class SampleHub : Hub
    {
        public async Task CallStaff(string compId, string tableId)
        {
            await Clients.All.ReceiveStaffCalling(compId, tableId);
        }

        public async Task StaffAccepted(string staffId)
        {
            await Clients.All.ReceiveStaffAccepted(staffId);
        }
    }
}
