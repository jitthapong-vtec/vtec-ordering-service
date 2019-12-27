using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using VerticalTec.POS.Share.LiveUpdate;
using VerticalTec.POS.Share.LiveUpdate.SignalRHubs;

namespace VerticalTec.POS.Service.LiveUpdateClient
{
    public class LiveUpdateClient : ILiveUpdateClient, IHostedService
    {
        HubConnection _hubConnection;

        public LiveUpdateClient(IConfiguration configure)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(configure.GetSection("AppSettings")["LiveUpdateHub"])
                .Build();
        }

        public Task ClientInfo(VersionInfo versionInfo)
        {
            throw new NotImplementedException();
        }

        public Task ReceiveUpdate(UpdateInfo updateInfo)
        {
            throw new NotImplementedException();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    await _hubConnection.StartAsync(cancellationToken);
                    break;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _hubConnection.DisposeAsync();
        }
    }
}
