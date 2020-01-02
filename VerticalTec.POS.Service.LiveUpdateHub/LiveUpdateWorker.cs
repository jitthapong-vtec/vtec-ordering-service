using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VerticalTec.POS.Share.LiveUpdate.SignalRHubs;

namespace VerticalTec.POS.Service.LiveUpdateHub
{
    public class LiveUpdateWorker : BackgroundService
    {
        readonly IHubContext<LiveUpdateHub, ILiveUpdateClient> _hubContext;

        public LiveUpdateWorker(IHubContext<LiveUpdateHub, ILiveUpdateClient> hubContext)
        {
            _hubContext = hubContext;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                _hubContext.Clients.All.ClientInfo()
            }
        }
    }
}
