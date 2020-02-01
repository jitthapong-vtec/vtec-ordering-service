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
            await Clients.Client(Context.ConnectionId).ReceiveConnectionEstablished();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await _consoleHub.Clients.All.ClientDisconnect(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendVersionDeploy(POSDataSetting posSetting)
        {
            using (var conn = await _db.ConnectAsync())
            {
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, posSetting.ShopID);
                await Clients.Client(Context.ConnectionId).ReceiveVersionDeploy(versionsDeploy);
            }
        }

        public Task ClientReceivedVersionDeploy()
        {
            return Clients.Client(Context.ConnectionId).ReceiveCmd(LiveUpdateCommands.SendVersionInfo);
        }

        public async Task ReceiveVersionInfo(VersionInfo versionInfo)
        {
            using(var conn = await _db.ConnectAsync())
            {
                versionInfo.SyncStatus = 1;
                await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                await _consoleHub.Clients.All.ClientUpdateInfo(versionInfo);
            }
        }

        public async Task ReceiveUpdateState(VersionLiveUpdate updateState)
        {
            using (var conn = await _db.ConnectAsync())
            {
                updateState.SyncStatus = 1;
                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);
            }
        }
    }
}