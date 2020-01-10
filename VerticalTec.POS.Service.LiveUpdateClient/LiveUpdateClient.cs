using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
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

        IDatabase _db;
        HubConnection _hubConnection;
        LiveUpdateDbContext _liveUpdateCtx;
        FrontConfigManager _frontConfigManager;

        public LiveUpdateClient(IDatabase db, LiveUpdateDbContext liveUpdateCtx, FrontConfigManager frontConfigManager)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;
            _frontConfigManager = frontConfigManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var isInitSuccess = false;
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

                            var liveUpdateConsoleUrl = await posRepo.GetPropertyValueAsync(conn, 1050, "LiveUpdateConsole");
                            if (!string.IsNullOrEmpty(liveUpdateConsoleUrl))
                            {
                                if (!liveUpdateConsoleUrl.EndsWith("/"))
                                    liveUpdateConsoleUrl += "/";
                                liveUpdateConsoleUrl += "hub";
                                InitHubConnection(liveUpdateConsoleUrl);
                                isInitSuccess = true;
                            }
                            else
                            {
                                _gbLogger.Error($"Not found parameter LiveUpdateConsole in property 1050");
                            }
                        }
                        catch (Exception ex)
                        {
                            _gbLogger.Error(ex, $"when try to load vTec-ResPOS.config => {ex.Message}");
                        }
                    }
                    else
                    {
                        _gbLogger.Error("Not found parameter VtecSoftwareRootPath in property 2004");
                    }
                }

                if (isInitSuccess)
                {
                    while (true)
                    {
                        try
                        {
                            await _hubConnection.StartAsync(cancellationToken);
                            _commLogger.Info("Connected to server");
                            break;
                        }
                        catch (Exception ex)
                        {
                            _gbLogger.Error(ex.Message);
                            await Task.Delay(1000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _gbLogger.Error(ex, ex.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                return _hubConnection.DisposeAsync();
            }
            catch
            {
                return Task.FromResult(true);
            }
        }

        private void InitHubConnection(string liveUpdateConsoleUrl)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(liveUpdateConsoleUrl)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On("SyncVersion", SyncVersion);
            _hubConnection.On<VersionInfo, VersionDeploy, VersionLiveUpdate, VersionLiveUpdateLog>("ReceiveSyncVersion", ReceiveSyncVersion);
        }

        public async Task SyncVersion()
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
                        ProgramVersion = programVersion?.FileVersion ?? "",
                        InsertDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    };
                    var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID, 1);
                    var versionLiveUpdateLog = await _liveUpdateCtx.GetVersionLiveUpdateLog(conn, posSetting.ShopID, posSetting.ComputerID, 1);

                    await _hubConnection.InvokeAsync("ReceiveSyncVersion", versionInfo, versionLiveUpdate, versionLiveUpdateLog);
                }
            }
            catch (Exception ex)
            {
                _commLogger.Error(ex, $"SyncVersion => {ex.Message}");
            }
        }

        public async Task ReceiveSyncVersion(VersionInfo versionInfo, VersionDeploy versionDeploy, VersionLiveUpdate versionLiveUpdate, VersionLiveUpdateLog liveUpdateLog)
        {
            try
            {
                try
                {
                    _commLogger.Info($"ReceiveSyncVersion => {JsonConvert.SerializeObject(versionInfo)}\n{JsonConvert.SerializeObject(versionDeploy)}\n{JsonConvert.SerializeObject(versionLiveUpdate)}\n{JsonConvert.SerializeObject(liveUpdateLog)}");
                }
                catch { }

                using (var conn = await _db.ConnectAsync())
                {
                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);
                    await _liveUpdateCtx.AddOrUpdateVersionDeploy(conn, versionDeploy);
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, liveUpdateLog);
                }
            }
            catch (Exception ex)
            {
                _gbLogger.Error(ex, "ReceiveSyncVersion");
            }
        }

        public Task UpdateVersion()
        {
            throw new NotImplementedException();
        }

        public Task ReceiveUpdateVersionState(VersionLiveUpdate versionLiveUpdate, VersionLiveUpdateLog liveUpdateLog)
        {
            throw new NotImplementedException();
        }
    }
}
