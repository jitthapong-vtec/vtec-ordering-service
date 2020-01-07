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
        static readonly NLog.Logger _commLogger = NLog.LogManager.GetLogger("communication");
        static readonly NLog.Logger _gbLogger = NLog.LogManager.GetLogger("global");

        HubConnection _hubConnection;
        IDatabase _db;
        LiveUpdateDbContext _liveUpdateCtx;
        FrontConfigManager _frontConfigManager;

        public LiveUpdateClient(IConfiguration configure, IDatabase db, LiveUpdateDbContext liveUpdateCtx, FrontConfigManager frontConfigManager)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;
            _frontConfigManager = frontConfigManager;

            var hubUri = new Uri(configure.GetSection("AppSettings")["LiveUpdateHub"]);
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUri)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On("SendVersionInfo", SendVersionInfo);
            _hubConnection.On<VersionInfo>("ReceiveSyncVersionInfo", ReceiveSyncVersionInfo);
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
                _commLogger.Error(ex, "ReceiveSyncVersionDeploy");
            }
        }

        public async Task ReceiveSyncVersionInfo(VersionInfo versionInfo)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);
                }
            }
            catch (Exception ex)
            {
                _gbLogger.Error(ex, $"ReceiveSyncVersionInfo => {ex.Message}");
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
                    var posSetting = _frontConfigManager.POSDataSetting;
                    var programVersion = await _liveUpdateCtx.GetFileVersion(conn, posSetting.ShopID, posSetting.ComputerID, "vTec-ResPOS.exe");
                    var versionInfo = new VersionInfo()
                    {
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramId = 1,
                        ProgramName = "vTec-ResPOS",
                        ProgramVersion = programVersion.FileVersion,
                        InsertDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    };
                    await _hubConnection.InvokeAsync("UpdateVersionInfo", versionInfo);
                }
            }
            catch (Exception ex)
            {
                _commLogger.Error(ex, $"SendVersionInfo => {ex.Message}");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var conn = await _db.ConnectAsync())
                {
                    await _liveUpdateCtx.UpdateStructure(conn);

                    var posRepo = new VtecPOSRepo(_db);
                    var rootPath = await posRepo.GetPropertyValueAsync(conn, 2004, "VtecSoftwareRootPath");
                    if (!string.IsNullOrEmpty(rootPath))
                    {
                        if (!rootPath.EndsWith("\\"))
                            rootPath += "\\";
                        var confPath = $"{rootPath}vTec-ResPOS\\vTec-ResPOS.config";
                        try
                        {
                            await _frontConfigManager.LoadConfig(confPath);
                        }
                        catch (Exception ex)
                        {
                            _gbLogger.Error($"when try to load vTec-ResPOS.config => {ex.Message}");
                        }
                    }
                    else
                    {
                        _gbLogger.Error("Not found property 2004(VtecSoftwareRootPath)");
                    }
                }

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
            catch (Exception ex)
            {
                _gbLogger.Error(ex, ex.Message);
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
