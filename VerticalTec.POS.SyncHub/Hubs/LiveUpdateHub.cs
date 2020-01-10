using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.SyncHub.Hubs
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
            await Clients.Client(Context.ConnectionId).SyncVersion();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task ReceiveSyncVersion(VersionInfo versionInfo, VersionLiveUpdate versionLiveUpdate, VersionLiveUpdateLog versionLiveUpdateLog)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    _logger.Info($"ReceiveSyncVersion from client {versionInfo.ComputerId}");
                    
                    versionInfo.SyncStatus = 1;
                    versionInfo.ConnectionId = Context.ConnectionId;
                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);
                    
                    var versionDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, 1, 1);
                    versionInfo = await _liveUpdateCtx.GetVersionInfo(conn, versionInfo.ShopId, versionInfo.ComputerId, versionInfo.ProgramId);
                    versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionInfo.ShopId, versionInfo.ComputerId, versionInfo.ProgramId);
                    versionLiveUpdateLog = await _liveUpdateCtx.GetVersionLiveUpdateLog(conn, versionInfo.ShopId, versionInfo.ComputerId, versionInfo.ProgramId);

                    if(versionInfo != null) 
                        versionInfo.SyncStatus = 1;
                    if(versionLiveUpdate != null) 
                        versionLiveUpdate.SyncStatus = 1;

                    await Clients.Client(Context.ConnectionId).ReceiveSyncVersion(versionInfo, versionDeploy, versionLiveUpdate, versionLiveUpdateLog);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"ReceiveSyncVersion => {ex.Message}");
            }
        }
    }
}