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

        IHubContext<ConsoleHub, IConsoleHub> _consoleHub;

        IDatabase _db;
        LiveUpdateDbContext _liveUpdateCtx;

        public LiveUpdateHub(IDatabase db, LiveUpdateDbContext liveUpdateCtx, IHubContext<ConsoleHub, IConsoleHub> consoleHub)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;
            _consoleHub = consoleHub;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.Info($"Client {Context.ConnectionId} connected");
            await Clients.Client(Context.ConnectionId).SyncVersion();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await _consoleHub.Clients.All.ClientDisconnect(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task ReceiveUpdateState(VersionLiveUpdate state, VersionLiveUpdateLog stateLog)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);
                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);

                state.SyncStatus = 1;
                await Clients.Client(Context.ConnectionId).ReceiveUpdateState(state);
            }
        }

        public async Task ReceiveSyncVersion(VersionInfo versionInfo, VersionLiveUpdate versionLiveUpdate, VersionLiveUpdateLog versionLiveUpdateLog)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    _logger.Info($"ReceiveSyncVersion from client {versionInfo.ComputerId}");

                    var versionDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, versionInfo.ShopId, versionInfo.ProgramId);

                    versionInfo.SyncStatus = 1;
                    versionInfo.ConnectionId = Context.ConnectionId;
                    versionInfo.ProgramVersion = versionDeploy.ProgramVersion;
                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                    if (versionLiveUpdate != null)
                    {
                        versionLiveUpdate.SyncStatus = 1;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);
                    }

                    if (versionLiveUpdateLog != null)
                    {
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, versionLiveUpdateLog);
                    }

                    versionInfo = await _liveUpdateCtx.GetVersionInfo(conn, versionInfo.ShopId, versionInfo.ComputerId, versionInfo.ProgramId);

                    await Clients.Client(Context.ConnectionId).ReceiveSyncVersion(versionInfo, versionDeploy);
                    await _consoleHub.Clients.All.ClientUpdateInfo(versionInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"ReceiveSyncVersion => {ex.Message}");
            }
        }
    }
}