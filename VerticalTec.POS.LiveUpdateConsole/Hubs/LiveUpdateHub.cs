using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.LiveUpdateConsole.Hubs
{
    public class LiveUpdateHub : Hub<ILiveUpdateClient>
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

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
            using (var conn = await _db.ConnectAsync())
            {
                var cmd = _db.CreateCommand("update versioninfo set IsOnline=0 where ConnectionId=@connectionId", conn);
                cmd.Parameters.Add(_db.CreateParameter("@connectionId", Context.ConnectionId));
                await _db.ExecuteNonQueryAsync(cmd);
            }
            await _consoleHub.Clients.All.ClientDisconnect(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendVersionDeploy(POSDataSetting posSetting)
        {
            using (var conn = await _db.ConnectAsync())
            {
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn);
                var versionDeploy = versionsDeploy.FirstOrDefault();
                var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID);
                await Clients.Client(Context.ConnectionId).ReceiveVersionDeploy(versionDeploy, versionLiveUpdate);
            }
        }

        public async Task UpdateVersionDeploy(VersionDeploy versionDeploy)
        {
            using(var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionDeploy(conn, versionDeploy);
            }
        }

        public Task ClientReceivedVersionDeploy()
        {
            return Clients.Client(Context.ConnectionId).ReceiveCmd(LiveUpdateCommands.SendVersionInfo);
        }

        public async Task ReceiveVersionInfo(VersionInfo versionInfo)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    versionInfo.ConnectionId = Context.ConnectionId;
                    versionInfo.SyncStatus = 1;
                    versionInfo.IsOnline = true;
                    versionInfo.UpdateDate = DateTime.Now;

                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                    await Clients.Client(Context.ConnectionId).ReceiveSyncVersion(versionInfo);
                    await _consoleHub.Clients.All.ClientUpdateInfo(versionInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ReceiveVersionInfo");
            }
        }

        public async Task ReceiveUpdateVersionState(VersionLiveUpdate updateState)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    updateState.SyncStatus = 1;
                    updateState.UpdateDate = DateTime.Now;

                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                    await Clients.Client(Context.ConnectionId).ReceiveSyncUpdateVersionState(updateState);
                    await _consoleHub.Clients.All.ClientUpdateVersionState(updateState);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ReceiveUpdateVersionState");
            }
        }
    }
}