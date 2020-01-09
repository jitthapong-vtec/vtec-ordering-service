using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VerticalTec.POS.LiveUpdate;
using VerticalTec.POS.LiveUpdate.Console.Hubs;

namespace VerticalTec.POS.LiveUpdate.Console.Services
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
            return Task.FromResult(true);
        }
    }
}
