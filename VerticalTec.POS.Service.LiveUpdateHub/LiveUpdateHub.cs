using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Share.LiveUpdate;
using VerticalTec.POS.Share.LiveUpdate.SignalRHubs;

namespace VerticalTec.POS.Service.LiveUpdateHub
{
    public class LiveUpdateHub : Hub<ILiveUpdateClient>
    {
        public LiveUpdateHub()
        {
        }

        public async Task UpdateClientInfo(VersionInfo info)
        {
        }
    }
}