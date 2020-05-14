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
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class LiveUpdateService : ILiveUpdateClient, IHostedService
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        IDatabase _db;
        IConfiguration _config;
        HubConnection _hubConnection;
        LiveUpdateDbContext _liveUpdateCtx;
        FrontConfigManager _frontConfigManager;
        VtecPOSEnv _vtecEnv;

        public LiveUpdateService(IDatabase db, IConfiguration config, LiveUpdateDbContext liveUpdateCtx,
            FrontConfigManager frontConfigManager, VtecPOSEnv posEnv)
        {
            _db = db;
            _config = config;
            _liveUpdateCtx = liveUpdateCtx;
            _frontConfigManager = frontConfigManager;

            _vtecEnv = posEnv;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var isInitSuccess = await IninitializeWorkingEnvironment();
            if (!isInitSuccess)
                return;
            try
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                _db.SetConnectionString($"Port={posSetting.DBPort};Connection Timeout=28800;Allow User Variables=True;default command timeout=28800;UID=vtecPOS;PASSWORD=vtecpwnet;SERVER={posSetting.DBIPServer};DATABASE={posSetting.DBName};old guids=true;");

                using (var conn = await _db.ConnectAsync())
                {
                    await _liveUpdateCtx.UpdateStructure(conn);

                    var posRepo = new VtecPOSRepo(_db);
                    var liveUpdateServer = await posRepo.GetPropertyValueAsync(conn, 1050, "LiveUpdateServer");
                    if (!string.IsNullOrEmpty(liveUpdateServer))
                    {
                        if (!liveUpdateServer.EndsWith("/"))
                            liveUpdateServer += "/";
                        liveUpdateServer += "liveupdate"; //hub endpoint
                        InitHubConnection(liveUpdateServer);
                        isInitSuccess = true;
                    }
                    else
                    {
                        _logger.LogError($"Not found parameter LiveUpdateHub in property 1050!!! This property needed to connect to live update server");
                    }
                }

                if (isInitSuccess)
                {
                    await StartHubConnection(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("StartAsync", ex);
            }
        }

        async Task<bool> IninitializeWorkingEnvironment()
        {
            var success = false;
            try
            {
                _logger.LogInfo("Initialize working environment...");

                var currentDir = Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).AbsolutePath));
                _vtecEnv.SoftwareRootPath = $"{Directory.GetParent(currentDir).FullName}\\";
                _vtecEnv.FrontCashierPath = $"{_vtecEnv.SoftwareRootPath}vTec-ResPOS\\";
                _vtecEnv.PatchDownloadPath = $"{_vtecEnv.SoftwareRootPath}Downloads\\";
                _vtecEnv.BackupPath = $"{_vtecEnv.SoftwareRootPath}Backup\\";

                try
                {
                    var liveUpdateAgentPath = currentDir;
                    var path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User);
                    if (path == null)
                    {
                        Environment.SetEnvironmentVariable("Path", "", EnvironmentVariableTarget.User);
                        path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User);
                    }
                    var updateAgentVar = path.Split(";").Where(p => p.EndsWith(liveUpdateAgentPath)).FirstOrDefault();
                    if (string.IsNullOrEmpty(updateAgentVar))
                    {
                        _logger.LogInfo("Create live update agent environment variable...");
                        Environment.SetEnvironmentVariable("Path", $"{path};{liveUpdateAgentPath}", EnvironmentVariableTarget.User);
                        _logger.LogInfo("Successfully create live update agent environment variable");
                    }
                }
                catch { }

                if (!Directory.Exists(_vtecEnv.PatchDownloadPath))
                    Directory.CreateDirectory(_vtecEnv.PatchDownloadPath);
                if (!Directory.Exists(_vtecEnv.BackupPath))
                    Directory.CreateDirectory(_vtecEnv.BackupPath);

                var confPath = $"{_vtecEnv.FrontCashierPath}vTec-ResPOS.config";
                Console.WriteLine($"Loading configuration file {confPath}");
                await _frontConfigManager.LoadConfig(confPath);

                var config = _frontConfigManager.POSDataSetting;
                _logger.LogInfo($"Successfully load configuration\nDBServer: {config.DBIPServer}\nDBName: {config.DBName}\nShopID: {config.ShopID}\nComputerID: {config.ComputerID}");

                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not load vTec-ResPOS.config", ex);
            }
            return success;
        }

        private void InitHubConnection(string liveUpdateServer)
        {
            _logger.LogInfo($"Initialize connection to live update server {liveUpdateServer}");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(liveUpdateServer)
                .WithAutomaticReconnect()
                .Build();
            _hubConnection.Reconnecting += Reconnecting;
            _hubConnection.Reconnected += Reconnected;
            _hubConnection.Closed += Closed;

            _hubConnection.On("ReceiveConnectionEstablished", ReceiveConnectionEstablished);
            _hubConnection.On<VersionDeploy, VersionLiveUpdate>("ReceiveVersionDeploy", ReceiveVersionDeploy);
            _hubConnection.On<VersionInfo>("ReceiveSyncVersion", ReceiveSyncVersion);
            _hubConnection.On<VersionLiveUpdate>("ReceiveSyncUpdateVersionState", ReceiveSyncUpdateVersionState);
            _hubConnection.On<LiveUpdateCommands, object>("ReceiveCmd", ReceiveCmd);
        }

        async Task StartHubConnection(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                try
                {
                    _logger.LogInfo("Connect to live update server...");

                    await _hubConnection.StartAsync(cancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Could not connect to live update server! {ex.Message}");
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

        private async Task Closed(Exception arg)
        {
            _logger.LogInfo($"Connecting closed {arg}");
            await StartHubConnection();
        }

        private Task Reconnected(string arg)
        {
            _logger.LogInfo($"Successfully reconnected {arg}");
            return Task.FromResult(true);
        }

        private Task Reconnecting(Exception arg)
        {
            _logger.LogInfo($"Try reconnecting...{arg}");
            return Task.FromResult(true);
        }

        public Task ReceiveConnectionEstablished()
        {
            _logger.LogInfo($"Yeh! Successfully connected to live update server");
            var posSetting = _frontConfigManager.POSDataSetting;
            // Told server to send version deploy info
            return _hubConnection.InvokeAsync("SendVersionDeploy", posSetting);
        }

        public async Task ReceiveVersionDeploy(VersionDeploy versionDeploy, VersionLiveUpdate versionLiveUpdate)
        {
            if (versionDeploy == null)
                return;

            using (var conn = await _db.ConnectAsync())
            {
                try
                {
                    await _liveUpdateCtx.AddOrUpdateVersionDeploy(conn, versionDeploy);
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);
                }
                catch (Exception ex)
                {
                    _logger.LogError("ReceiveVersionDeploy", ex);
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
                var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID);
                var fileName = versionLiveUpdate.ProgramId == ProgramTypes.Front ? "vTec-ResPOS.exe" : "";
                var fileVersion = await _liveUpdateCtx.GetFileVersion(conn, posSetting.ShopID, posSetting.ComputerID, fileName);
                var lastVersion = "0";
                if (fileVersion != null)
                {
                    lastVersion = fileVersion.FileVersion;
                }
                else
                {
                    _logger.Error("Not found fileversion of vTec-ResPOS.exe");
                }

                var versionsInfo = await _liveUpdateCtx.GetVersionInfo(conn, posSetting.ShopID, posSetting.ComputerID, versionLiveUpdate.ProgramId);
                var versionInfo = versionsInfo.FirstOrDefault();
                if (versionInfo == null)
                {
                    versionInfo = new VersionInfo()
                    {
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramName = versionLiveUpdate.ProgramName,
                        ProgramId = versionLiveUpdate.ProgramId,
                        InsertDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    };
                }
                versionInfo.ProgramVersion = lastVersion;

                await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                // Told server to update client version info
                await _hubConnection.InvokeAsync("ReceiveVersionInfo", versionInfo);
            }
        }

        public async Task ReceiveSyncVersion(VersionInfo versionInfo)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionInfo.ShopId, versionInfo.ComputerId);

                if (versionLiveUpdate.FileReceiveStatus == FileReceiveStatus.NoReceivedFile)
                    await DownloadFile();
                else
                    await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", versionLiveUpdate);
            }
        }

        public async Task ReceiveSyncUpdateVersionState(VersionLiveUpdate versionLiveUpdate)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);
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

                var updateState = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID);

                var versionsDeploys = await _liveUpdateCtx.GetVersionDeploy(conn, updateState.BatchId);
                var versionDeploy = versionsDeploys.FirstOrDefault();

                var downloadService = new DownloadService(_config.GetValue<string>("GoogleDriveApiKey"));
                var updateStateLog = new VersionLiveUpdateLog()
                {
                    ShopId = posSetting.ShopID,
                    ComputerId = posSetting.ComputerID,
                    ProgramVersion = versionDeploy.ProgramVersion
                };

                var stepLog = "Start download";
                _logger.LogInfo(stepLog);
                try
                {
                    updateState.RevStartTime = DateTime.Now;
                    updateState.MessageLog = stepLog;
                    updateState.FileReceiveStatus = FileReceiveStatus.Downloading;
                    updateState.LiveUpdateCmd = LiveUpdateCommands.DownloadFile;
                    updateState.CommandStatus = CommandStatus.Start;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                    updateStateLog.LogMessage = stepLog;
                    updateStateLog.ActionStatus = 1;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);
                    await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", updateState);

                    var fileId = UrlParameterExtensions.GetValue(versionDeploy.FileUrl, "id");
                    var result = await downloadService.DownloadFile(fileId, _vtecEnv.PatchDownloadPath);
                    if (result.Success)
                    {
                        stepLog = "Download complete";
                        _logger.LogInfo(stepLog);

                        updateState.FileReceiveStatus = FileReceiveStatus.Downloaded;
                        updateState.DownloadFilePath = _vtecEnv.PatchDownloadPath + result.FileName;
                        updateState.RevEndTime = DateTime.Now;
                        updateState.MessageLog = stepLog;
                        updateState.CommandStatus = CommandStatus.Finish;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                        updateStateLog.LogMessage = stepLog;
                        updateStateLog.EndTime = DateTime.Now;
                        updateStateLog.ActionStatus = 2;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);
                        await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", updateState);

                        if (versionDeploy.AutoBackup)
                            await BackupFile();
                    }
                    else
                    {
                        stepLog = "Download failed";
                        _logger.LogInfo(stepLog);

                        updateState.MessageLog = stepLog;
                        updateState.FileReceiveStatus = FileReceiveStatus.NoReceivedFile;
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
                    updateState.FileReceiveStatus = FileReceiveStatus.NoReceivedFile;
                    updateState.CommandStatus = CommandStatus.Error;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                    updateStateLog.LogMessage = stepLog;
                    updateStateLog.EndTime = DateTime.Now;
                    updateStateLog.ActionStatus = 99;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);

                    await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", updateState);

                    _logger.LogError("Download file", ex);
                }
            }
        }

        async Task BackupFile()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var state = await _liveUpdateCtx.GetVersionLiveUpdate(conn, posSetting.ShopID, posSetting.ComputerID);

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

                    var backupFileName = $"{_vtecEnv.BackupPath}{state.ProgramName}{DateTime.Now.ToString("yyyyMMddHH")}.zip";
                    stepLog = $"Start backup {backupFileName}";
                    _logger.LogInfo(stepLog);

                    stateLog.LogMessage = stepLog;
                    stateLog.ActionStatus = 1;
                    stateLog.StartTime = DateTime.Now;

                    state.BackupStartTime = DateTime.Now;
                    state.BackupStatus = BackupStatus.BackingUp;
                    state.LiveUpdateCmd = LiveUpdateCommands.BackupFile;
                    state.CommandStatus = CommandStatus.Start;
                    state.MessageLog = stepLog;

                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);
                    await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", state);

                    if (File.Exists(backupFileName))
                        File.Delete(backupFileName);

                    ZipFile.CreateFromDirectory(_vtecEnv.FrontCashierPath, backupFileName);

                    stepLog = $"Backup {backupFileName} finish";
                    _logger.LogInfo(stepLog);

                    state.BackupEndTime = DateTime.Now;
                    state.BackupStatus = BackupStatus.BackupFinish;
                    state.BackupFilePath = backupFileName;
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
                    state.BackupStatus = BackupStatus.NoBackup;
                    state.CommandStatus = CommandStatus.Error;
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                    stateLog.ActionStatus = 99;
                    stateLog.LogMessage = $"Backup error {ex.Message}";
                    stateLog.EndTime = DateTime.Now;

                    _logger.LogError(stateLog.LogMessage, ex);

                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);
                    await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", state);
                }
            }
        }
    }
}
