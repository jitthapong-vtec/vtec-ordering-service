using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin.Models
{
    public class StaffCommunicationHub : Hub
    {
        public async Task CallStaff(string compId, string tableName)
        {
            await Clients.All.OnReceiveStaffCalling(compId, tableName);
        }

        public async Task StaffAccepted(string staffId)
        {
            await Clients.All.OnReceiveStaffAccepted(staffId);
        }
    }
}
