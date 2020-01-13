using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Net;
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
        IConfiguration _config;
        HubConnection _hubConnection;
        LiveUpdateDbContext _liveUpdateCtx;
        FrontConfigManager _frontConfigManager;

        string _vtSoftwareRootPath;
        string _frontCashierPath;
        string _patchDownloadPath;
        string _backupPath;

        public LiveUpdateClient(IDatabase db, IConfiguration config, LiveUpdateDbContext liveUpdateCtx, FrontConfigManager frontConfigManager)
        {
            _db = db;
            _liveUpdateCtx = liveUpdateCtx;
            _frontConfigManager = frontConfigManager;
            _config = config;
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
                    _vtSoftwareRootPath = await posRepo.GetPropertyValueAsync(conn, 2004, "VtecSoftwareRootPath");
                    if (!string.IsNullOrEmpty(_vtSoftwareRootPath))
                    {
                        if (!_vtSoftwareRootPath.EndsWith("\\"))
                            _vtSoftwareRootPath += "\\";
                        _frontCashierPath = $"{_vtSoftwareRootPath}vTec-ResPOS\\";
                        _patchDownloadPath = $"{_vtSoftwareRootPath}Downloads\\";
                        _backupPath = $"{_vtSoftwareRootPath}Backup\\";

                        if (!Directory.Exists(_patchDownloadPath))
                            Directory.CreateDirectory(_patchDownloadPath);
                        if (!Directory.Exists(_backupPath))
                            Directory.CreateDirectory(_backupPath);

                        var confPath = $"{_frontCashierPath}vTec-ResPOS.config";
                        try
                        {
                            await _frontConfigManager.LoadConfig(confPath);

                            var liveUpdateHub = await posRepo.GetPropertyValueAsync(conn, 1050, "LiveUpdateHub");
                            if (!string.IsNullOrEmpty(liveUpdateHub))
                            {
                                InitHubConnection(liveUpdateHub);
                                isInitSuccess = true;
                            }
                            else
                            {
                                _gbLogger.Error($"Not found parameter LiveUpdateHub in property 1050");
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
            _hubConnection.On<VersionInfo, VersionDeploy>("ReceiveSyncVersion", ReceiveSyncVersion);
            _hubConnection.On<VersionLiveUpdate, VersionLiveUpdateLog>("ReceiveUpdateVersionState", ReceiveUpdateVersionState);
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
                        ProgramName = "vTec-ResPOS",
                        ProgramVersion = programVersion?.FileVersion ?? "",
                        InsertDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    };
                    var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID);
                    var versionLiveUpdateLog = await _liveUpdateCtx.GetVersionLiveUpdateLog(conn, posSetting.ShopID, posSetting.ComputerID);

                    await _hubConnection.InvokeAsync("ReceiveSyncVersion", versionInfo, versionLiveUpdate, versionLiveUpdateLog);
                }
            }
            catch (Exception ex)
            {
                _commLogger.Error(ex, $"SyncVersion => {ex.Message}");
            }
        }

        public async Task ReceiveSyncVersion(VersionInfo versionInfo, VersionDeploy versionDeploy)
        {
            try
            {
                _commLogger.Info($"ReceiveSyncVersion");

                using (var conn = await _db.ConnectAsync())
                {
                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);
                    await _liveUpdateCtx.AddOrUpdateVersionDeploy(conn, versionDeploy);
                }

                await CheckUpdate();
            }
            catch (Exception ex)
            {
                _gbLogger.Error(ex, "ReceiveSyncVersion");
            }
        }

        public async Task CheckUpdate()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var versionDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, posSetting.ShopID);
                if (versionDeploy == null)
                    return;

                var versionInfo = await _liveUpdateCtx.GetVersionInfo(conn, posSetting.ShopID, posSetting.ComputerID);
                if (versionInfo == null)
                    return;

                if (versionDeploy.ProgramVersion == versionInfo.ProgramVersion)
                    return;

                var updateState = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID);
                var updateStateLog = await _liveUpdateCtx.GetVersionLiveUpdateLog(conn, posSetting.ShopID, posSetting.ComputerID);

                updateState ??= new VersionLiveUpdate()
                {
                    ShopId = posSetting.ShopID,
                    ComputerId = posSetting.ComputerID,
                    ProgramName = versionDeploy.ProgramName,
                    UpdateVersion = versionDeploy.ProgramVersion
                };
                updateStateLog ??= new VersionLiveUpdateLog()
                {
                    ShopId = posSetting.ShopID,
                    ComputerId = posSetting.ComputerID,
                    ProgramVersion = versionDeploy.ProgramVersion
                };

                var receivedFile = updateState.RevFile == 1;
                if (!receivedFile)
                {
                    var downloadService = new DownloadService(_config);
                    try
                    {
                        updateState.RevStartTime = DateTime.Now;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                        updateStateLog.LogMessage = $"Start download {versionDeploy.FileId}";
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                        await _hubConnection.InvokeAsync("ReceiveUpdateState", updateState, updateStateLog);

                        var downloadFile = $"{_patchDownloadPath}patch.zip";
                        var result = await downloadService.DownloadFile(versionDeploy.FileId, downloadFile);
                        if (result.Status == Google.Apis.Download.DownloadStatus.Completed)
                        {
                            updateState.RevFile = 1;
                            updateState.RevEndTime = DateTime.Now;
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                            updateStateLog.LogMessage = $"Download {versionDeploy.FileId} complete";
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                            await _hubConnection.InvokeAsync("ReceiveUpdateState", updateState, updateStateLog);

                            await Backup(updateState);
                        }
                        else if (result.Status == Google.Apis.Download.DownloadStatus.Failed)
                        {
                            updateState.MessageLog = "Download failed";
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                            updateStateLog.LogMessage = updateState.MessageLog;
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                            await _hubConnection.InvokeAsync("ReceiveUpdateState", updateState, updateStateLog);
                        }
                    }
                    catch (Exception ex)
                    {
                        _commLogger.Error(ex, "Download file");
                    }
                }
                else
                {
                    await Backup(updateState);
                }
            }
        }

        async Task Backup(VersionLiveUpdate state)
        {
            using (var conn = await _db.ConnectAsync())
            {
                var stateLog = new VersionLiveUpdateLog()
                {
                    ShopId = state.ShopId,
                    ComputerId = state.ComputerId,
                    ProgramVersion = state.UpdateVersion
                };

                try
                {
                    state.BackupStartTime = DateTime.Now;
                    state.BackupStatus = 1;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                    var backupFileName = $"{_backupPath}{DateTime.Now.ToString("yyyyMMdd")}.zip";
                    stateLog.LogMessage = $"Start backup {backupFileName}";
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);

                    ZipFile.CreateFromDirectory(_frontCashierPath, backupFileName);

                    state.BackupEndTime = DateTime.Now;
                    state.BackupStatus = 2;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                    stateLog.LogMessage = $"Backup {backupFileName} finish";
                    stateLog.EndTime = DateTime.Now;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);

                    await _hubConnection.InvokeAsync("ReceiveUpdateState", state, stateLog);
                }
                catch (Exception ex)
                {
                    _commLogger.Error(ex, "Backup");
                    state.MessageLog = ex.Message;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                    stateLog.LogMessage = $"Backup error {ex.Message}";
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);
                    await _hubConnection.InvokeAsync("ReceiveUpdateState", state, stateLog);
                }
            }
        }

        public async Task ReceiveUpdateVersionState(VersionLiveUpdate versionLiveUpdate, VersionLiveUpdateLog liveUpdateLog)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);
                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, liveUpdateLog);
            }
        }
    }
}
