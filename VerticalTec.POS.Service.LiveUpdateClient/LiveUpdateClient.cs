using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        HubConnection _hubConnection;
        LiveUpdateDbContext _liveUpdateCtx;
        FrontConfigManager _frontConfigManager;

        string _vtSoftwareRootPath;
        string _frontCashierPath;
        string _patchDownloadPath;
        string _backupPath;

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

            _hubConnection.On("ReceiveConnectionEstablished", ReceiveConnectionEstablished);
            _hubConnection.On<List<VersionDeploy>>("ReceiveVersionDeploy", ReceiveVersionDeploy);
            _hubConnection.On<LiveUpdateCommands, object>("ReceiveCmd", ReceiveCmd);
        }

        public Task ReceiveConnectionEstablished()
        {
            var posSetting = _frontConfigManager.POSDataSetting;
            return _hubConnection.InvokeAsync("SendVersionDeploy", posSetting);
        }

        public async Task ReceiveVersionDeploy(List<VersionDeploy> versionsDeploy)
        {
            if (!versionsDeploy.Any())
                return;

            using (var conn = await _db.ConnectAsync())
            {
                foreach (var versionDeploy in versionsDeploy)
                {
                    await _liveUpdateCtx.AddOrUpdateVersionDeploy(conn, versionDeploy);
                }
                await _hubConnection.InvokeAsync("ClientReceivedVersionDeploy");
            }
        }

        async Task SendVersionInfo(string connectionId)
        {
            using(var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, posSetting.ShopID);
                foreach(var versionDeploy in versionsDeploy)
                {
                    var versionsInfo = await _liveUpdateCtx.GetVersionInfo(conn, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);
                    var versionInfo = versionsInfo.FirstOrDefault();
                    versionInfo ??= new VersionInfo()
                    {
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramName = versionDeploy.ProgramName,
                        ProgramVersion = versionDeploy.ProgramVersion,
                        ProgramId = versionDeploy.ProgramId,
                        InsertDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    };
                    versionInfo.ConnectionId = connectionId;
                    versionInfo.IsOnline = true;

                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);
                    await _hubConnection.InvokeAsync("ReceiveVersionInfo", versionInfo);

                    var updateState = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);
                    await _hubConnection.InvokeAsync("ReceiveUpdateState", updateState);
                }
            }
        }

        public async Task ReceiveCmd(LiveUpdateCommands cmd, object param)
        {
            switch (cmd)
            {
                case LiveUpdateCommands.SendVersionInfo:
                    await SendVersionInfo(param.ToString());
                    break;
                case LiveUpdateCommands.UpdateVersion:
                    await UpdateVersion();
                    break;
                case LiveUpdateCommands.BackupFile:
                    await Backup();
                    break;
            }
        }

        async Task UpdateVersion()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, posSetting.ShopID);
                if (!versionsDeploy.Any())
                    return;

                foreach (var versionDeploy in versionsDeploy)
                {
                    var versionInfo = await _liveUpdateCtx.GetVersionInfo(conn, posSetting.ShopID, posSetting.ComputerID, versionDeploy.ProgramId);
                    if (!versionInfo.Any())
                        return;

                    if (versionDeploy.ProgramVersion == versionInfo.FirstOrDefault().ProgramVersion)
                        return;

                    var updateState = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID, versionDeploy.ProgramId);

                    updateState ??= new VersionLiveUpdate()
                    {
                        BatchId = versionDeploy.BatchId,
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramName = versionDeploy.ProgramName,
                        UpdateVersion = versionDeploy.ProgramVersion
                    };

                    var receivedFile = updateState.RevFile == 1;
                    if (!receivedFile)
                    {
                        var downloadService = new DownloadService(versionDeploy.GoogleDriveApiKey);
                        var updateStateLog = new VersionLiveUpdateLog()
                        {
                            ShopId = posSetting.ShopID,
                            ComputerId = posSetting.ComputerID,
                            ProgramVersion = versionDeploy.ProgramVersion
                        };
                        try
                        {
                            updateState.RevStartTime = DateTime.Now;
                            updateState.MessageLog = "Start download";
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                            updateStateLog.LogMessage = $"Start download";
                            updateStateLog.ActionStatus = 1;
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);
                            await _hubConnection.InvokeAsync("ReceiveUpdateState", updateState, updateStateLog);

                            var downloadFile = $"{_patchDownloadPath}";
                            var result = await downloadService.DownloadFile(versionDeploy.GoogleDriveFileId, downloadFile);
                            if (result.Status == Google.Apis.Download.DownloadStatus.Completed)
                            {
                                updateState.RevFile = 1;
                                updateState.RevEndTime = DateTime.Now;
                                updateState.MessageLog = "Download complete";
                                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                                updateStateLog.LogMessage = $"Download complete";
                                updateStateLog.EndTime = DateTime.Now;
                                updateStateLog.ActionStatus = 2;
                                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                                await _hubConnection.InvokeAsync("ReceiveUpdateState", updateState, updateStateLog);

                                await Backup();
                            }
                            else if (result.Status == Google.Apis.Download.DownloadStatus.Failed)
                            {
                                updateState.MessageLog = "Download failed";
                                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                                updateStateLog.LogMessage = updateState.MessageLog;
                                updateStateLog.EndTime = DateTime.Now;
                                updateStateLog.ActionStatus = 99;
                                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                                await _hubConnection.InvokeAsync("ReceiveUpdateState", updateState, updateStateLog);
                            }
                        }
                        catch (Exception ex)
                        {
                            updateState.MessageLog = $"Download failed {ex.Message}";
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                            updateStateLog.LogMessage = updateState.MessageLog;
                            updateStateLog.EndTime = DateTime.Now;
                            updateStateLog.ActionStatus = 99;
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                            await _hubConnection.InvokeAsync("ReceiveUpdateState", updateState, updateStateLog);

                            _gbLogger.Error(ex, "Download file");
                        }
                    }
                    else
                    {
                        await Backup();
                    }
                }
            }
        }

        async Task Backup()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, posSetting.ShopID);

                foreach (var versionDeploy in versionsDeploy)
                {
                    var state = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID, versionDeploy.ProgramId);

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

                        var backupFileName = $"{_backupPath}{state.ProgramName}{DateTime.Now.ToString("yyyyMMdd")}.zip";
                        stateLog.LogMessage = $"Start backup {backupFileName}";
                        stateLog.ActionStatus = 1;
                        stateLog.StartTime = DateTime.Now;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);

                        if (File.Exists(backupFileName))
                            File.Delete(backupFileName);

                        ZipFile.CreateFromDirectory(_frontCashierPath, backupFileName);

                        state.BackupEndTime = DateTime.Now;
                        state.BackupStatus = 2;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                        stateLog.LogMessage = $"Backup {backupFileName} finish";
                        stateLog.EndTime = DateTime.Now;
                        stateLog.ActionStatus = 2;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);

                        await _hubConnection.InvokeAsync("ReceiveUpdateState", state, stateLog);
                    }
                    catch (Exception ex)
                    {
                        _commLogger.Error(ex, "Backup");
                        state.MessageLog = ex.Message;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                        stateLog.ActionStatus = 99;
                        stateLog.LogMessage = $"Backup error {ex.Message}";
                        stateLog.EndTime = DateTime.Now;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);
                        await _hubConnection.InvokeAsync("ReceiveUpdateState", state, stateLog);
                    }
                }
            }
        }
    }
}
