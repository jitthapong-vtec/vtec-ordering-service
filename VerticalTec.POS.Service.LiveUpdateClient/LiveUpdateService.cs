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

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class LiveUpdateService : ILiveUpdateClient, IHostedService
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

        public LiveUpdateService(IDatabase db, IConfiguration config, LiveUpdateDbContext liveUpdateCtx, FrontConfigManager frontConfigManager)
        {
            _db = db;
            _config = config;
            _liveUpdateCtx = liveUpdateCtx;
            _frontConfigManager = frontConfigManager;
        }

        async Task<bool> IninitializeWorkingEnvironment()
        {
            var success = false;
            try
            {
                var currentDir = Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).AbsolutePath));
                _vtSoftwareRootPath = $"{Directory.GetParent(currentDir).FullName}\\";
                _frontCashierPath = $"{_vtSoftwareRootPath}vTec-ResPOS\\";
                _patchDownloadPath = $"{_vtSoftwareRootPath}Downloads\\";
                _backupPath = $"{_vtSoftwareRootPath}Backup\\";

                if (!Directory.Exists(_patchDownloadPath))
                    Directory.CreateDirectory(_patchDownloadPath);
                if (!Directory.Exists(_backupPath))
                    Directory.CreateDirectory(_backupPath);

                var confPath = $"{_frontCashierPath}vTec-ResPOS.config";
                await _frontConfigManager.LoadConfig(confPath);
            }
            catch (Exception ex)
            {
                _gbLogger.Error(ex, $"Try to load vTec-ResPOS.config");
            }
            return success;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var isInitSuccess = await IninitializeWorkingEnvironment();
            try
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                _db.SetConnectionString($"Port={posSetting.DBPort};Connection Timeout=28800;Allow User Variables=True;default command timeout=28800;UID=vtecPOS;PASSWORD=vtecpwnet;SERVER={posSetting.DBIPServer};DATABASE={posSetting.DBName};old guids=true;");

                using (var conn = await _db.ConnectAsync())
                {
                    await _liveUpdateCtx.UpdateStructure(conn);

                    var posRepo = new VtecPOSRepo(_db);
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

                if (isInitSuccess)
                {
                    await StartHubConnection(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _gbLogger.Error(ex, ex.Message);
            }
        }

        async Task StartHubConnection(CancellationToken cancellationToken = default)
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
            _hubConnection.Reconnecting += Reconnecting;
            _hubConnection.Reconnected += Reconnected;
            _hubConnection.Closed += Closed;

            _hubConnection.On("ReceiveConnectionEstablished", ReceiveConnectionEstablished);
            _hubConnection.On<List<VersionDeploy>>("ReceiveVersionDeploy", ReceiveVersionDeploy);
            _hubConnection.On<VersionInfo>("ReceiveSyncVersion", ReceiveSyncVersion);
            _hubConnection.On<VersionLiveUpdate>("ReceiveSyncUpdateVersionState", ReceiveSyncUpdateVersionState);
            _hubConnection.On<LiveUpdateCommands, object>("ReceiveCmd", ReceiveCmd);
        }

        private async Task Closed(Exception arg)
        {
            _commLogger.Info($"Connecting closed {arg}");
            await StartHubConnection();
        }

        private Task Reconnected(string arg)
        {
            _commLogger.Info($"Reconnected {arg}");
            return Task.FromResult(true);
        }

        private Task Reconnecting(Exception arg)
        {
            _commLogger.Info($"Reconnecting...{arg}");
            return Task.FromResult(true);
        }

        public Task ReceiveConnectionEstablished()
        {
            var posSetting = _frontConfigManager.POSDataSetting;
            // Told server to send version deploy info
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
                // Told server whether already received version deploy info
                // after server received version deploy info, it will send some cmd to client for send back current version info to it 
                await _hubConnection.InvokeAsync("ClientReceivedVersionDeploy");
            }
        }

        async Task SendVersionInfo()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, posSetting.ShopID);
                foreach (var versionDeploy in versionsDeploy)
                {
                    var versionsInfo = await _liveUpdateCtx.GetVersionInfo(conn, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);
                    var versionInfo = versionsInfo.FirstOrDefault();
                    if (versionInfo == null)
                    {
                        var fileName = versionDeploy.ProgramId == ProgramTypes.Front ? "vTec-ResPOS.exe" : "";
                        var fileVersion = await _liveUpdateCtx.GetFileVersion(conn, versionDeploy.ShopId, posSetting.ComputerID, fileName);
                        if (fileVersion == null)
                            _gbLogger.Error("Not found fileversion of vTec-ResPOS.exe");

                        versionInfo = new VersionInfo()
                        {
                            ShopId = posSetting.ShopID,
                            ComputerId = posSetting.ComputerID,
                            ProgramName = versionDeploy.ProgramName,
                            ProgramVersion = fileVersion?.FileVersion,
                            ProgramId = versionDeploy.ProgramId,
                            InsertDate = DateTime.Now,
                            UpdateDate = DateTime.Now
                        };
                    }

                    await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                    // Told server to update client version info
                    await _hubConnection.InvokeAsync("ReceiveVersionInfo", versionInfo);

                    // Told server to receive update state
                    // after this step the server will call ReceiveSyncVersion to update sync state
                    var updateState = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);
                    await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", updateState);
                }
            }
        }

        public async Task ReceiveSyncVersion(VersionInfo versionInfo)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionInfo.ShopId, versionInfo.ComputerId, versionInfo.ProgramId);

                var isUpdateAvailble = false;
                if (versionLiveUpdate != null)
                {
                    if (!Utils.CompareVersion(versionLiveUpdate.UpdateVersion, versionInfo.ProgramVersion))
                    {
                        isUpdateAvailble = versionLiveUpdate.RevFile == 0;
                    }
                }
                else
                {
                    isUpdateAvailble = true;
                }

                if (isUpdateAvailble)
                {
                    await DownloadFile();
                }
            }
        }

        public async Task ReceiveSyncUpdateVersionState(VersionLiveUpdate state)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);
            }
        }

        public async Task ReceiveCmd(LiveUpdateCommands cmd, object param)
        {
            switch (cmd)
            {
                case LiveUpdateCommands.SendVersionInfo:
                    await SendVersionInfo();
                    break;
                case LiveUpdateCommands.DownloadFile:
                    await DownloadFile();
                    break;
                case LiveUpdateCommands.BackupFile:
                    await BackupFile();
                    break;
            }
        }

        async Task DownloadFile()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, posSetting.ShopID);
                if (!versionsDeploy.Any())
                    return;

                foreach (var versionDeploy in versionsDeploy)
                {
                    if (versionDeploy.BatchStatus < 1)
                        continue;

                    var versionInfo = await _liveUpdateCtx.GetVersionInfo(conn, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);
                    if (!versionInfo.Any())
                        return;

                    var updateState = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);

                    updateState ??= new VersionLiveUpdate()
                    {
                        BatchId = versionDeploy.BatchId,
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramId = versionDeploy.ProgramId,
                        ProgramName = versionDeploy.ProgramName,
                        UpdateVersion = versionDeploy.ProgramVersion
                    };

                    var downloadService = new DownloadService(_config.GetValue<string>("GoogleDriveApiKey"));
                    var updateStateLog = new VersionLiveUpdateLog()
                    {
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramVersion = versionDeploy.ProgramVersion
                    };

                    var stepLog = "Start download";
                    try
                    {
                        updateState.RevStartTime = DateTime.Now;
                        updateState.MessageLog = stepLog;
                        updateState.CommandStatus = CommandStatus.Start;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                        updateStateLog.LogMessage = stepLog;
                        updateStateLog.ActionStatus = 1;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);
                        await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", updateState);

                        var result = await downloadService.DownloadFile(versionDeploy.GoogleDriveFileId, _patchDownloadPath);
                        if (result.Status == Google.Apis.Download.DownloadStatus.Completed)
                        {
                            stepLog = "Download complete";
                            updateState.RevFile = 1;
                            updateState.RevEndTime = DateTime.Now;
                            updateState.MessageLog = stepLog;
                            updateState.CommandStatus = CommandStatus.Finish;
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                            updateStateLog.LogMessage = stepLog;
                            updateStateLog.EndTime = DateTime.Now;
                            updateStateLog.ActionStatus = 2;
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);
                            await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", updateState);

                            await BackupFile();
                        }
                        else if (result.Status == Google.Apis.Download.DownloadStatus.Failed)
                        {
                            stepLog = "Download failed";
                            updateState.MessageLog = stepLog;
                            updateState.CommandStatus = CommandStatus.Finish;
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                            updateStateLog.LogMessage = stepLog;
                            updateStateLog.EndTime = DateTime.Now;
                            updateStateLog.ActionStatus = 99;
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                            await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", updateState);
                        }
                    }
                    catch (Exception ex)
                    {
                        stepLog = $"Download failed {ex.Message}";
                        updateState.MessageLog = stepLog;
                        updateState.CommandStatus = CommandStatus.Finish;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                        updateStateLog.LogMessage = stepLog;
                        updateStateLog.EndTime = DateTime.Now;
                        updateStateLog.ActionStatus = 99;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                        await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", updateState);

                        _gbLogger.Error(ex, "Download file");
                    }
                }
            }
        }

        async Task BackupFile()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, posSetting.ShopID);

                foreach (var versionDeploy in versionsDeploy)
                {
                    var state = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID, versionDeploy.ProgramId);
                    state ??= new VersionLiveUpdate()
                    {
                        BatchId = versionDeploy.BatchId,
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramId = versionDeploy.ProgramId,
                        ProgramName = versionDeploy.ProgramName,
                        UpdateVersion = versionDeploy.ProgramVersion
                    };

                    var stateLog = new VersionLiveUpdateLog()
                    {
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramId = state.ProgramId,
                        ProgramVersion = state.UpdateVersion
                    };

                    try
                    {
                        var stepLog = "";

                        var backupFileName = $"{_backupPath}{state.ProgramName}{DateTime.Now.ToString("yyyyMMdd")}.zip";
                        stepLog = $"Start backup {backupFileName}"; ;

                        stateLog.LogMessage = stepLog;
                        stateLog.ActionStatus = 1;
                        stateLog.StartTime = DateTime.Now;

                        state.BackupStartTime = DateTime.Now;
                        state.BackupStatus = 1;
                        state.CommandStatus = CommandStatus.Start;
                        state.MessageLog = stepLog;

                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);
                        await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", state);

                        if (File.Exists(backupFileName))
                            File.Delete(backupFileName);

                        ZipFile.CreateFromDirectory(_frontCashierPath, backupFileName);

                        stepLog = $"Backup {backupFileName} finish";

                        state.BackupEndTime = DateTime.Now;
                        state.BackupStatus = 2;
                        state.CommandStatus = CommandStatus.Finish;
                        state.MessageLog = stepLog;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                        stateLog.LogMessage = stepLog;
                        stateLog.EndTime = DateTime.Now;
                        stateLog.ActionStatus = 2;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);

                        await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", state);
                    }
                    catch (Exception ex)
                    {
                        state.MessageLog = ex.Message;
                        state.CommandStatus = CommandStatus.Finish;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                        stateLog.ActionStatus = 99;
                        stateLog.LogMessage = $"Backup error {ex.Message}";
                        stateLog.EndTime = DateTime.Now;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);
                        await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", state);
                    }
                }
            }
        }
    }
}
