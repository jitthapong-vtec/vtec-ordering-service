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
            await Clients.Client(Context.ConnectionId).OnConnected();
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

        public async Task RequestVersionDeploy(POSDataSetting posSetting)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    var brandId = 0;
                    var cmd = _db.CreateCommand(conn);
                    cmd.CommandText = "select BrandID from shop_data where ShopID=@shopId";
                    cmd.Parameters.Add(_db.CreateParameter("@shopId", posSetting.ShopID));
                    using (var reader = await _db.ExecuteReaderAsync(cmd))
                    {
                        if (reader.Read())
                        {
                            brandId = reader.GetValue<int>("BrandID");
                        }
                    }

                    var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn);
                    var versionDeploy = versionsDeploy.Where(v => v.BatchStatus == VersionDeployBatchStatus.Actived && v.BrandId == brandId).FirstOrDefault();

                    await Clients.Client(Context.ConnectionId).ReceiveVersionDeploy(versionDeploy);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "RequestVersionDeploy");
            }
        }

        public async Task ReceiveVersionLiveUpdate(VersionLiveUpdate versionLiveUpdate)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    if (versionLiveUpdate != null)
                    {
                        versionLiveUpdate.SyncStatus = 1;
                        versionLiveUpdate.UpdateDate = DateTime.Now;

                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);
                    }

                    await Clients.Client(Context.ConnectionId).ReceiveVersionLiveUpdate(versionLiveUpdate);
                    await _consoleHub.Clients.All.ClientUpdateVersionState(versionLiveUpdate);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ReceiveUpdateVersionState");
            }
        }

        public async Task UpdateVersionDeploy(VersionDeploy versionDeploy)
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
                _logger.Error(ex, "UpdateVersionDeploy");
            }
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

                    await Clients.Client(Context.ConnectionId).ReceiveVersionInfo(versionInfo);
                    await _consoleHub.Clients.All.ClientUpdateInfo(versionInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ReceiveVersionInfo");
            }
        }


    }
}