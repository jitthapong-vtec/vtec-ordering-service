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
        static readonly NLog.Logger _commLogger = NLog.LogManager.GetLogger("communication");
        static readonly NLog.Logger _gbLogger = NLog.LogManager.GetLogger("global");

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
                    var liveUpdateHub = await posRepo.GetPropertyValueAsync(conn, 1050, "LiveUpdateHub");
                    if (!string.IsNullOrEmpty(liveUpdateHub))
                    {
                        InitHubConnection(liveUpdateHub);
                        isInitSuccess = true;
                    }
                    else
                    {
                        _gbLogger.LogError($"Not found parameter LiveUpdateHub in property 1050!!! This property needed to connect to live update server");
                    }
                }

                if (isInitSuccess)
                {
                    await StartHubConnection(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _gbLogger.LogError("StartAsync", ex);
            }
        }

        async Task<bool> IninitializeWorkingEnvironment()
        {
            var success = false;
            try
            {
                _gbLogger.LogInfo("Initialize working environment...");

                var currentDir = Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).AbsolutePath));
                _vtecEnv.SoftwareRootPath = $"{Directory.GetParent(currentDir).FullName}\\";
                _vtecEnv.FrontCashierPath = $"{_vtecEnv.SoftwareRootPath}vTec-ResPOS\\";
                _vtecEnv.PatchDownloadPath = $"{_vtecEnv.SoftwareRootPath}Downloads\\";
                _vtecEnv.BackupPath = $"{_vtecEnv.SoftwareRootPath}Backup\\";

                try
                {
                    var liveUpdateAgentPath = currentDir;//"D:\\Vtec\\Source\\VerticalTec.POS\\VerticalTec.POS.Service.LiveUpdateAgent\\bin\\Debug";
                    var path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User);
                    var updateAgentVar = path.Split(";").Where(p => p.EndsWith(liveUpdateAgentPath)).FirstOrDefault();
                    if (string.IsNullOrEmpty(updateAgentVar))
                    {
                        _gbLogger.LogInfo("Create live update agent environment variable...");//$"{_vtSoftwareRootPath}\\LiveUpdateAgent\\VtecLiveUpdateAgent.exe"
                        Environment.SetEnvironmentVariable("Path", $"{path};{liveUpdateAgentPath}", EnvironmentVariableTarget.User);
                        _gbLogger.LogInfo("Successfully create live update agent environment variable");
                    }
                }
                catch (Exception ex)
                {

                }

                if (!Directory.Exists(_vtecEnv.PatchDownloadPath))
                    Directory.CreateDirectory(_vtecEnv.PatchDownloadPath);
                if (!Directory.Exists(_vtecEnv.BackupPath))
                    Directory.CreateDirectory(_vtecEnv.BackupPath);

                var confPath = $"{_vtecEnv.FrontCashierPath}vTec-ResPOS.config";
                Console.WriteLine($"Loading configuration file {confPath}");
                await _frontConfigManager.LoadConfig(confPath);

                var config = _frontConfigManager.POSDataSetting;
                _gbLogger.LogInfo($"Successfully load configuration\nDBServer: {config.DBIPServer}\nDBName: {config.DBName}\nShopID: {config.ShopID}\nComputerID: {config.ComputerID}");

                success = true;
            }
            catch (Exception ex)
            {
                _gbLogger.LogError($"Could not load vTec-ResPOS.config", ex);
            }
            return success;
        }

        private void InitHubConnection(string liveUpdateConsoleUrl)
        {
            _gbLogger.LogInfo($"Initialize connection to live update server {liveUpdateConsoleUrl}");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(liveUpdateConsoleUrl)
                .WithAutomaticReconnect()
                .Build();
            _hubConnection.Reconnecting += Reconnecting;
            _hubConnection.Reconnected += Reconnected;
            _hubConnection.Closed += Closed;

            _hubConnection.On("ReceiveConnectionEstablished", ReceiveConnectionEstablished);
            _hubConnection.On<List<VersionDeploy>>("ReceiveVersionDeploy", ReceiveVersionDeploy);
            _hubConnection.On<VersionDeploy, VersionInfo>("ReceiveSyncVersion", ReceiveSyncVersion);
            _hubConnection.On<VersionLiveUpdate>("ReceiveSyncUpdateVersionState", ReceiveSyncUpdateVersionState);
            _hubConnection.On<LiveUpdateCommands, object>("ReceiveCmd", ReceiveCmd);
        }

        async Task StartHubConnection(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                try
                {
                    _commLogger.LogInfo("Connect to live update server...");

                    await _hubConnection.StartAsync(cancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    _commLogger.LogError($"Could not connect to live update server! {ex.Message}");
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
            _commLogger.LogInfo($"Connecting closed {arg}");
            await StartHubConnection();
        }

        private Task Reconnected(string arg)
        {
            _commLogger.LogInfo($"Successfully reconnected {arg}");
            return Task.FromResult(true);
        }

        private Task Reconnecting(Exception arg)
        {
            _commLogger.LogInfo($"Try reconnecting...{arg}");
            return Task.FromResult(true);
        }

        public Task ReceiveConnectionEstablished()
        {
            _commLogger.LogInfo($"Yeh! Successfully connected to live update server");
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
                try
                {
                    foreach (var versionDeploy in versionsDeploy)
                    {
                        await _liveUpdateCtx.AddOrUpdateVersionDeploy(conn, versionDeploy);
                    }
                }
                catch (Exception ex)
                {
                    _gbLogger.LogError("ReceiveVersionDeploy", ex);
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
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, shopId: posSetting.ShopID);
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
                    await _hubConnection.InvokeAsync("ReceiveVersionInfo", versionDeploy, versionInfo);
                }
            }
        }

        public async Task ReceiveSyncVersion(VersionDeploy versionDeploy, VersionInfo versionInfo)
        {
            using (var conn = await _db.ConnectAsync())
            {
                await _liveUpdateCtx.AddOrUpdateVersionInfo(conn, versionInfo);

                var versionLiveUpdate = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy.BatchId, versionInfo.ShopId, versionInfo.ComputerId, versionInfo.ProgramId);
                if (versionLiveUpdate == null)
                {
                    var postSetting = _frontConfigManager.POSDataSetting;
                    versionLiveUpdate = new VersionLiveUpdate()
                    {
                        BatchId = versionDeploy.BatchId,
                        BranId = versionDeploy.BrandId,
                        ShopId = versionDeploy.ShopId,
                        ComputerId = postSetting.ComputerID,
                        ProgramId = versionDeploy.ProgramId,
                        ProgramName = versionDeploy.ProgramName,
                        UpdateVersion = versionDeploy.ProgramVersion,
                        InsertDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    };
                    await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, versionLiveUpdate);
                }

                if (versionLiveUpdate.RevFile == 0)
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
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, shopId: posSetting.ShopID);
                if (!versionsDeploy.Any())
                    return;

                foreach (var versionDeploy in versionsDeploy.Where(v => v.BatchStatus == VersionDeployBatchStatus.Actived))
                {
                    var versionInfo = await _liveUpdateCtx.GetVersionInfo(conn, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);
                    if (!versionInfo.Any())
                        return;

                    var updateState = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy.BatchId, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);

                    if (updateState == null)
                    {
                        updateState = new VersionLiveUpdate()
                        {
                            BatchId = versionDeploy.BatchId,
                            ShopId = posSetting.ShopID,
                            ComputerId = posSetting.ComputerID,
                            ProgramId = versionDeploy.ProgramId,
                            ProgramName = versionDeploy.ProgramName,
                            UpdateVersion = versionDeploy.ProgramVersion
                        };
                    }

                    var downloadService = new DownloadService(_config.GetValue<string>("GoogleDriveApiKey"));
                    var updateStateLog = new VersionLiveUpdateLog()
                    {
                        ShopId = posSetting.ShopID,
                        ComputerId = posSetting.ComputerID,
                        ProgramVersion = versionDeploy.ProgramVersion
                    };

                    var stepLog = "Start download";
                    _commLogger.LogInfo(stepLog);
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

                        var fileId = UrlParameterExtensions.GetValue(versionDeploy.FileUrl, "id");
                        var result = await downloadService.DownloadFile(fileId, _vtecEnv.PatchDownloadPath);
                        if (result.Success)
                        {
                            stepLog = "Download complete";
                            _commLogger.LogInfo(stepLog);

                            updateState.RevFile = 1;
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
                            _commLogger.LogInfo(stepLog);

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

                        _gbLogger.LogError("Download file", ex);
                    }
                }
            }
        }

        async Task BackupFile()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var posSetting = _frontConfigManager.POSDataSetting;
                var versionsDeploy = await _liveUpdateCtx.GetVersionDeploy(conn, shopId:posSetting.ShopID);

                foreach (var versionDeploy in versionsDeploy)
                {
                    var state = await _liveUpdateCtx.GetVersionLiveUpdate(conn, versionDeploy.BatchId, versionDeploy.ShopId, posSetting.ComputerID, versionDeploy.ProgramId);
                    if (state == null)
                    {
                        state = new VersionLiveUpdate()
                        {
                            BatchId = versionDeploy.BatchId,
                            ShopId = posSetting.ShopID,
                            ComputerId = posSetting.ComputerID,
                            ProgramId = versionDeploy.ProgramId,
                            ProgramName = versionDeploy.ProgramName,
                            UpdateVersion = versionDeploy.ProgramVersion
                        };
                    }

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

                        var backupFileName = $"{_vtecEnv.BackupPath}{state.ProgramName}{DateTime.Now.ToString("yyyyMMdd")}.zip";
                        stepLog = $"Start backup {backupFileName}";
                        _commLogger.LogInfo(stepLog);

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

                        ZipFile.CreateFromDirectory(_vtecEnv.FrontCashierPath, backupFileName);

                        stepLog = $"Backup {backupFileName} finish";
                        _commLogger.LogInfo(stepLog);

                        state.BackupEndTime = DateTime.Now;
                        state.BackupStatus = 2;
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
                        state.CommandStatus = CommandStatus.Finish;
                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdate(conn, state);

                        stateLog.ActionStatus = 99;
                        stateLog.LogMessage = $"Backup error {ex.Message}";
                        stateLog.EndTime = DateTime.Now;

                        _gbLogger.LogError(stateLog.LogMessage, ex);

                        await _liveUpdateCtx.AddOrUpdateVersionLiveUpdateLog(conn, stateLog);
                        await _hubConnection.InvokeAsync("ReceiveUpdateVersionState", state);
                    }
                }
            }
        }
    }
}
