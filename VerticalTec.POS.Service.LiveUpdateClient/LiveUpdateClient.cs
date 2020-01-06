using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.Service.LiveUpdateClient
{
    public class LiveUpdateClient : ILiveUpdateClient, IHostedService
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetLogger("communication");

        HubConnection _hubConnection;
        IDatabase _db;
        LiveUpdateDbContext _liveUpdateCtx;

        public LiveUpdateClient(IConfiguration configure, IDatabase db, LiveUpdateDbContext liveUpdateCtx)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;

            var hubUri = new Uri(configure.GetSection("AppSettings")["LiveUpdateHub"]);
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUri)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On("SendVersionInfo", SendVersionInfo);
            _hubConnection.On<VersionDeploy>("ReceiveSyncVersionDeploy", ReceiveSyncVersionDeploy);

        }

        public Task CancelUpdate()
        {
            throw new NotImplementedException();
        }

        public async Task ReceiveSyncVersionDeploy(VersionDeploy versionDeploy)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    await _liveUpdateCtx.AddOrUpdateVersionDeploy(conn, versionDeploy);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ReceiveSyncVersionDeploy");
            }
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
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var versionInfo = _liveUpdateCtx.GetVersionInfo(conn, 0, 0, 0);
                    await _hubConnection.InvokeAsync("UpdateVersionInfo", versionInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"SendVersionInfo => {ex.Message}");
            }
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
                catch (Exception e)
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
