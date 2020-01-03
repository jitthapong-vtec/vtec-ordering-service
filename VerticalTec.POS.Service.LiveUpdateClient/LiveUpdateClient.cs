using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using VerticalTec.POS.Share.LiveUpdate;

namespace VerticalTec.POS.Service.LiveUpdateClient
{
    public class LiveUpdateClient : ILiveUpdateClient, IHostedService
    {
        HubConnection _hubConnection;

        public LiveUpdateClient(IConfiguration configure)
        {
            var hubUri = new Uri(configure.GetSection("AppSettings")["LiveUpdateHub"]);
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUri)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On("SendVersionInfo", SendVersionInfo);
        }

        public Task CancelUpdate()
        {
            throw new NotImplementedException();
        }

        public Task ReceiveUpdateStatus(VersionLiveUpdate liveUpdate)
        {
            throw new NotImplementedException();
        }

        public Task SendUpdateStatus()
        {
            throw new NotImplementedException();
        }

        public async Task SendVersionInfo()
        {
            var versionInfo = new VersionInfo();
            await _hubConnection.InvokeAsync("UpdateVersionInfo", versionInfo);
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
                catch(Exception e)
                {
                    await Task.Delay(1000);
                }
            }
        }

        public Task StartUpdate()
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _hubConnection.DisposeAsync();
        }
    }
}
