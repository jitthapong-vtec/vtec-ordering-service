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
        IClientConnectionService _connectionService;
        IDbstructureUpdateService _dbStructureUpdateService;
        IDownloadService _downloadService;
        LiveUpdateDbContext _liveUpdateCtx;
        FrontConfigManager _frontConfigManager;
        VtecPOSEnv _vtecEnv;
        BackupService _backupService;

        public LiveUpdateService(IDatabase db, IClientConnectionService clientConnectionService, 
            IDbstructureUpdateService dbStrucUpdateService, IDownloadService downloadService,
            LiveUpdateDbContext liveUpdateCtx, FrontConfigManager frontConfigManager, VtecPOSEnv posEnv, BackupService backupService)
        {
            _db = db;
            _connectionService = clientConnectionService;
            _dbStructureUpdateService = dbStrucUpdateService;
            _downloadService = downloadService;
            _liveUpdateCtx = liveUpdateCtx;
            _frontConfigManager = frontConfigManager;

            _vtecEnv = posEnv;
            _backupService = backupService;
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
                _vtecEnv.SoftwareRootPath = @$"{Directory.GetParent(currentDir).FullName}\";
                _vtecEnv.FrontCashierPath = @$"{_vtecEnv.SoftwareRootPath}vTec-ResPOS\";
                _vtecEnv.PatchDownloadPath = @$"{_vtecEnv.SoftwareRootPath}Downloads\";
                _vtecEnv.BackupPath = @$"{_vtecEnv.SoftwareRootPath}Backup\";

                try
                {
                    var liveUpdateAgentPath = Path.Combine(Directory.GetParent(currentDir).FullName, "vTec Live Update", "vTec Live Update Agent");
                    _logger.Info($"liveUpdateAgentPath => {liveUpdateAgentPath}");
                    var path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
                    if (path == null)
                    {
                        Environment.SetEnvironmentVariable("Path", "", EnvironmentVariableTarget.Machine);
                        path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
                    }
                    _logger.Info($"path => {path}");
                    var updateAgentVar = path.Split(";").Where(p => p.EndsWith(liveUpdateAgentPath)).FirstOrDefault();
                    _logger.Info($"config upate agent path => {updateAgentVar}");
                    if (string.IsNullOrEmpty(updateAgentVar))
                    {
                        _logger.LogInfo("Create live update agent environment variable...");
                        Environment.SetEnvironmentVariable("Path", $"{path};{liveUpdateAgentPath}", EnvironmentVariableTarget.Machine);
                        _logger.LogInfo("Successfully create live update agent environment variable");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Initial working environment error => {ex.Message}");
                }

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

            _connectionService.InitConnection(liveUpdateServer);
            _connectionService.Subscribe("OnConnected", OnConnected);
            _connectionService.Subscribe<VersionDeploy>("ReceiveVersionDeploy", ReceiveVersionDeploy);
            _connectionService.Subscribe<VersionInfo>("ReceiveVersionInfo", ReceiveVersionInfo);
            _connectionService.Subscribe<VersionLiveUpdate>("ReceiveVersionLiveUpdate", ReceiveVersionLiveUpdate);
            _connectionService.Subscribe<LiveUpdateCommands, object>("ReceiveCmd", ReceiveCmd);
        }

        async Task StartHubConnection(CancellationToken cancellationToken = default)
        {
            await _connectionService.StartConnectionAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _connectionService.StopConnectionAsync(cancellationToken);
        }

        public async Task OnConnected()
        {
            _logger.LogInfo($"Yeh! Successfully connected to live update server");
            await SendVersionInfo();
        }

        async Task SendVersionInfo()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var versionDeploy = await _liveUpdateCtx.GetActiveVersionDeploy(conn);

                var posSetting = _frontConfigManager.POSDataSetting;
                var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy?.BatchId, posSetting.ShopID, posSetting.ComputerID);

                var fileName = "vTec-ResPOS.exe";
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

                var versionsInfo = await _liveUpdateCtx.GetVersionInfo(conn, posSetting.ShopID, posSetting.ComputerID);
                var versionInfo = versionsInfo.FirstOrDefault();
                if (versionInfo == null)
                {
                    versionInfo = new VersionInfo()
                    {
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramName = versionLiveUpdate?.ProgramName ?? "vTec-ResPOS",
                        ProgramId = versionLiveUpdate?.ProgramId ?? ProgramTypes.Front,
                        InsertDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    };
                }
                versionInfo.ProgramVersion = lastVersion;

                await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                await _connectionService.HubConnection.InvokeAsync("ReceiveVersionInfo", versionInfo);
            }
        }

        public async Task ReceiveVersionInfo(VersionInfo versionInfo)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                var posSetting = _frontConfigManager.POSDataSetting;
                await _connectionService.HubConnection.InvokeAsync("RequestVersionDeploy", posSetting);
            }
        }


        public async Task ReceiveVersionDeploy(VersionDeploy versionDeploy)
        {
            using (var conn = await _db.ConnectAsync())
            {
                try
                {
                    var cmd = _db.CreateCommand("delete from version_deploy", conn);
                    await _db.ExecuteNonQueryAsync(cmd);

                    if (versionDeploy != null)
                    {
                        await _liveUpdateCtx.AddOrUpdateVersionDeploy(conn, versionDeploy);

                        var posSetting = _frontConfigManager.POSDataSetting;
                        var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy?.BatchId, posSetting.ShopID, posSetting.ComputerID);
                        if (versionLiveUpdate != null)
                        {
                            await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", versionLiveUpdate);
                        }
                        else
                        {
                            var liveUpdate = CreateLiveUpdateObject(versionDeploy, posSetting);
                            await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, liveUpdate);

                            await DownloadFile();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("ReceiveVersionDeploy", ex);
                }
            }
        }

        public async Task ReceiveVersionLiveUpdate(VersionLiveUpdate versionLiveUpdate)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);

                if (versionLiveUpdate?.FileReceiveStatus == FileReceiveStatus.NoReceivedFile)
                {
                    await DownloadFile();
                }
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

                var versionDeploy = await _liveUpdateCtx.GetActiveVersionDeploy(conn);
                if (versionDeploy == null)
                    return;

                var updateState = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy.BatchId, posSetting.ShopID, posSetting.ComputerID);
                if (updateState == null)
                {
                    updateState = CreateLiveUpdateObject(versionDeploy, posSetting);
                }
                updateState.ReadyToUpdate = 0;

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
                    await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", updateState);

                    _downloadService.Cancel();
                    _downloadService.DownloadFile(versionDeploy.FileUrl, _vtecEnv.PatchDownloadPath);

                    if (_downloadService.IsComplete)
                    {
                        stepLog = "Download complete";
                        _logger.LogInfo(stepLog);

                        updateState.FileReceiveStatus = FileReceiveStatus.Downloaded;
                        updateState.DownloadFilePath = _vtecEnv.PatchDownloadPath + _downloadService.FileName;
                        updateState.RevEndTime = DateTime.Now;
                        updateState.MessageLog = stepLog;
                        updateState.CommandStatus = CommandStatus.Finish;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, updateState);

                        updateStateLog.LogMessage = stepLog;
                        updateStateLog.EndTime = DateTime.Now;
                        updateStateLog.ActionStatus = 2;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, updateStateLog);
                        await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", updateState);

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

                        await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", updateState);
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

                    await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", updateState);

                    _logger.LogError("Download file", ex);
                }
            }
        }

        async Task BackupFile()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;

                var versionDeploy = await _liveUpdateCtx.GetActiveVersionDeploy(conn);
                if (versionDeploy == null)
                {
                    await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", null);
                    return;
                }

                var state = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy.BatchId, posSetting.ShopID, posSetting.ComputerID);

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
                    await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", state);

                    _backupService.Backup(state.DownloadFilePath, backupFileName);

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

                    await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", state);
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
                    await _connectionService.HubConnection.InvokeAsync("ReceiveVersionLiveUpdate", state);
                }
            }
        }

        private VersionLiveUpdate CreateLiveUpdateObject(VersionDeploy versionDeploy, POSDataSetting posSetting)
        {
            return new VersionLiveUpdate()
            {
                BatchId = versionDeploy.BatchId,
                ShopId = posSetting.ShopID,
                ComputerId = posSetting.ComputerID,
                ProgramId = ProgramTypes.Front,
                ProgramName = versionDeploy?.ProgramName,
                UpdateVersion = versionDeploy?.ProgramVersion
            };
        }
    }
}
