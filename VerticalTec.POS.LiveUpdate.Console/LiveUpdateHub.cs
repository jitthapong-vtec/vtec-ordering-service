using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.LiveUpdate.Console
{
    public class LiveUpdateHub : Hub<ILiveUpdateClient>
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetLogger("communication");

        IDatabase _db;
        LiveUpdateDbContext _liveUpdateCtx;

        public LiveUpdateHub(IDatabase db, LiveUpdateDbContext liveUpdateCtx)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.Info($"Client {Context.ConnectionId} connected");
            await Clients.Client(Context.ConnectionId).SendVersionInfo();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task UpdateVersionInfo(VersionInfo info)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    _logger.Info($"Receive versioninfo from {info.ComputerId}");
                    info.SyncStatus = 1;
                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, info);
                    info = await _liveUpdateCtx.GetVersionInfo(conn, info.ShopId, info.ComputerId, info.ProgramId);
                    await Clients.Client(Context.ConnectionId).ReceiveSyncVersionInfo(info);

                    var versionDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, 1, 1);
                    if (versionDeploy != null)
                    {
                        _logger.Info($"Send version deploy back to {info.ComputerId}");
                        await Clients.Client(Context.ConnectionId).ReceiveSyncVersionDeploy(versionDeploy);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"UpdateVersionInfo => {ex.Message}");
            }
        }
    }
}