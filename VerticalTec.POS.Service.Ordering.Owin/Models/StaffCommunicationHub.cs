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
        public async Task CallStaff(int compId, int tableId, string tableName)
        {
            await Clients.All.OnReceiveStaffCalling(compId, tableId, tableName);
        }

        public async Task StaffAcknowledge(int staffId, int compId, int tableId)
        {
            await Clients.All.OnReceiveStaffAcknowledge(staffId, compId, tableId);
        }
    }
}
