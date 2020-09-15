using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.LiveUpdate
{
    public class LiveUpdateDbContext
    {
        IDatabase _db;

        public LiveUpdateDbContext(IDatabase db)
        {
            _db = db;
        }

        public async Task UpdateStructure(IDbConnection conn)
        {
            var tbs = new string[] {
                @"CREATE TABLE Version_Deploy (
               BatchID VARCHAR(50) NOT NULL,
               BrandID INT NOT NULL,
               ProgramID SMALLINT NOT NULL,
               ProgramName VARCHAR(100) NOT NULL,
               ProgramVersion VARCHAR(20) NOT NULL,
               FileUrl VARCHAR(255),
               BatchStatus TINYINT NOT NULL DEFAULT '0',
               AutoBackup TINYINT NOT NULL DEFAULT '0',
               ScheduleUpdate DATETIME NULL,
               InsertDate DATETIME NULL,
               UpdateDate DATETIME NULL,
               PRIMARY KEY(BatchID)
            );",
            @"CREATE TABLE VersionInfo (
               ShopID INT NOT NULL,
               ComputerID INT NOT NULL,
               ProgramID SMALLINT NOT NULL,
               ProgramName VARCHAR(100) NOT NULL,
               ProgramVersion VARCHAR(20) NOT NULL,
               VersionStatus TINYINT NOT NULL,
               InsertDate DATETIME NOT NULL,
               UpdateDate DATETIME NOT NULL,
               SyncStatus TINYINT NOT NULL DEFAULT '0',
               ConnectionId VARCHAR(50) NOT NULL,
               PRIMARY KEY (ShopID,ComputerID,ProgramID)
            );",
                @"CREATE TABLE Version_LiveUpdate (
               BatchID VARCHAR(50) NOT NULL,
               ShopID INT NOT NULL,
               ComputerID INT NOT NULL,
               ProgramID SMALLINT NOT NULL,
               ProgramName VARCHAR(100) NOT NULL,
               UpdateVersion VARCHAR(20) NOT NULL,
               FileReceiveStatus TINYINT NOT NULL DEFAULT '0',
               DownloadFilePath VARCHAR(255) NULL,
               RevStartTime DATETIME NULL,
               RevEndTime DATETIME NULL,
               BackupStatus TINYINT NOT NULL DEFAULT '0',
               BackupFilePath VARCHAR(255) NULL,
               BackupStartTime DATETIME NULL,
               BackupEndTime DATETIME NULL,
               ScheduleUpdate DATETIME NULL,
               UpdateStartTime DATETIME NULL,
               UpdateEndTime DATETIME NULL,
               RollbackStatus TINYINT NOT NULL DEFAULT '0',
               UpdateStatus TINYINT NOT NULL DEFAULT '0',
               SyncStatus TINYINT NOT NULL DEFAULT '0',
               ReadyToUpdate TINYINT NOT NULL DEFAULT '0',
               MessageLog VARCHAR(2000) NULL,
               InsertDate DATETIME NOT NULL,
               UpdateDate DATETIME NOT NULL,
               PRIMARY KEY(BatchID, ShopID, ComputerID, ProgramID)
            );",
            @"CREATE TABLE Version_LiveUpdateLog (
               LogUUID VARCHAR(50) NOT NULL,
               SaleDate DATETIME NOT NULL,
               ShopID INT NOT NULL,
               ComputerID INT NOT NULL,
               ProgramID SMALLINT NOT NULL,
               ActionID INT NOT NULL,
               ProgramVersion VARCHAR(20) NOT NULL,
               ActionStatus TINYINT NOT NULL,
               StartTime DATETIME NULL,
               EndTime DATETIME NULL,
               LogMessage VARCHAR(2000) NULL,
               PRIMARY KEY (LogUUID)
            );"
            };

            foreach (var sql in tbs)
            {
                try
                {
                    var cmd = _db.CreateCommand(sql, conn);
                    await _db.ExecuteNonQueryAsync(cmd);
                }
                catch { }
            }
        }

        public async Task<VersionLiveUpdate> GetVersionLiveUpdate(IDbConnection conn, string batchId, int shopId, int computerId, ProgramTypes types = ProgramTypes.Front)
        {
            var cmd = _db.CreateCommand("select * from version_liveupdate where BatchID=@batchId and ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@batchId", batchId));
            cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", computerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", (int)types));

            VersionLiveUpdate versionLiveUpdate = null;
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    versionLiveUpdate = new VersionLiveUpdate()
                    {
                        BatchId = reader.GetValue<string>("BatchID"),
                        ShopId = reader.GetValue<int>("ShopID"),
                        ComputerId = reader.GetValue<int>("ComputerID"),
                        ProgramId = (ProgramTypes)reader.GetValue<int>("ProgramID"),
                        ProgramName = reader.GetValue<string>("ProgramName"),
                        UpdateVersion = reader.GetValue<string>("UpdateVersion"),
                        FileReceiveStatus = (FileReceiveStatus)reader.GetValue<int>("FileReceiveStatus"),
                        DownloadFilePath = reader.GetValue<string>("DownloadFilePath"),
                        RevStartTime = reader.GetValue<DateTime>("RevStartTime"),
                        RevEndTime = reader.GetValue<DateTime>("RevEndTime"),
                        BackupStatus = (BackupStatus)reader.GetValue<int>("BackupStatus"),
                        BackupFilePath = reader.GetValue<string>("BackupFilePath"),
                        BackupStartTime = reader.GetValue<DateTime>("BackupStartTime"),
                        BackupEndTime = reader.GetValue<DateTime>("BackupEndTime"),
                        ScheduleUpdate = reader.GetValue<DateTime>("ScheduleUpdate"),
                        SyncStatus = reader.GetValue<int>("SyncStatus"),
                        ReadyToUpdate = reader.GetValue<int>("ReadyToUpdate"),
                        UpdateStatus = reader.GetValue<int>("UpdateStatus"),
                        MessageLog = reader.GetValue<string>("MessageLog"),
                        InsertDate = reader.GetValue<DateTime>("InsertDate"),
                        UpdateDate = reader.GetValue<DateTime>("UpdateDate")
                    };
                }
            }
            return versionLiveUpdate;
        }

        public async Task<List<VersionLiveUpdateLog>> GetVersionLiveUpdateLog(IDbConnection conn, int shopId, int computerId)
        {
            var cmd = _db.CreateCommand("select * from Version_LiveUpdateLog where ShopID=@shopId and ComputerID=@computerId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", computerId));

            List<VersionLiveUpdateLog> logs = new List<VersionLiveUpdateLog>();
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                while (reader.Read())
                {
                    logs.Add(new VersionLiveUpdateLog()
                    {
                        LogUUID = reader.GetValue<string>("LogUUID"),
                        SaleDate = reader.GetValue<DateTime>("SaleDate"),
                        ShopId = reader.GetValue<int>("ShopID"),
                        ComputerId = reader.GetValue<int>("ComputerID"),
                        ProgramId = (ProgramTypes)reader.GetValue<int>("ProgramID"),
                        ActionId = reader.GetValue<int>("ActionID"),
                        ProgramVersion = reader.GetValue<string>("ProgramVersion"),
                        ActionStatus = reader.GetValue<int>("ActionStatus"),
                        StartTime = reader.GetValue<DateTime>("StartTime"),
                        EndTime = reader.GetValue<DateTime>("EndTime"),
                        LogMessage = reader.GetValue<string>("LogMessage")
                    });
                }
            }
            return logs;
        }

        public async Task<List<VersionDeploy>> GetVersionDeploy(IDbConnection conn, string batchId = "")
        {
            var cmd = _db.CreateCommand(conn);
            cmd.CommandText = "select * from Version_Deploy where BatchStatus != 99";

            if (!string.IsNullOrWhiteSpace(batchId))
            {
                cmd.CommandText += " and BatchID=@batchId";
                cmd.Parameters.Add(_db.CreateParameter("@batchId", batchId));
            }

            List<VersionDeploy> versionsDeploy = new List<VersionDeploy>();
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                while (reader.Read())
                {
                    versionsDeploy.Add(new VersionDeploy()
                    {
                        BatchId = reader.GetValue<string>("BatchID"),
                        BrandId = reader.GetValue<int>("BrandID"),
                        ProgramId = (ProgramTypes)reader.GetValue<int>("ProgramID"),
                        ProgramName = reader.GetValue<string>("ProgramName"),
                        ProgramVersion = reader.GetValue<string>("ProgramVersion"),
                        FileUrl = reader.GetValue<string>("FileUrl"),
                        BatchStatus = (VersionDeployBatchStatus)reader.GetValue<int>("BatchStatus"),
                        AutoBackup = reader.GetValue<bool>("AutoBackup"),
                        ScheduleUpdate = reader.GetValue<DateTime>("ScheduleUpdate"),
                        InsertDate = reader.GetValue<DateTime>("InsertDate"),
                        UpdateDate = reader.GetValue<DateTime>("UpdateDate")
                    });
                }
            }
            return versionsDeploy;
        }

        public async Task<List<VersionInfo>> GetVersionInfo(IDbConnection conn, int shopId = 0, int computerId = 0, ProgramTypes types = ProgramTypes.All)
        {
            var cmd = _db.CreateCommand("select a.*, b.ComputerName, c.ShopCode, c.ShopName" +
                " from VersionInfo a" +
                " left join computername b" +
                " on a.ComputerID=b.ComputerID" +
                " left join shop_data c" +
                " on a.ShopID=c.ShopID" +
                " where 1=1", conn);

            if (shopId > 0)
            {
                cmd.CommandText += " and a.ShopID=@shopId";
                cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
            }
            if (computerId > 0)
            {
                cmd.CommandText += " and a.ComputerID=@computerId";
                cmd.Parameters.Add(_db.CreateParameter("@computerId", computerId));
            }
            if (types != ProgramTypes.All)
            {
                cmd.CommandText += " and a.ProgramID=@programId";
                cmd.Parameters.Add(_db.CreateParameter("@programId", (int)types));
            }

            List<VersionInfo> versionsInfo = new List<VersionInfo>();
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                while (reader.Read())
                {
                    versionsInfo.Add(new VersionInfo()
                    {
                        ShopId = reader.GetValue<int>("ShopID"),
                        ComputerId = reader.GetValue<int>("ComputerID"),
                        ProgramId = (ProgramTypes)reader.GetValue<int>("ProgramID"),
                        ConnectionId = reader.GetValue<string>("ConnectionId"),
                        ProgramName = reader.GetValue<string>("ProgramName"),
                        ComputerName = reader.GetValue<string>("ComputerName"),
                        ShopCode = reader.GetValue<string>("ShopCode"),
                        ShopName = reader.GetValue<string>("ShopName"),
                        ProgramVersion = reader.GetValue<string>("ProgramVersion"),
                        VersionStatus = reader.GetValue<int>("VersionStatus"),
                        InsertDate = reader.GetValue<DateTime>("InsertDate"),
                        UpdateDate = reader.GetValue<DateTime>("UpdateDate"),
                        SyncStatus = reader.GetValue<int>("SyncStatus")
                    });
                }
            }
            return versionsInfo;
        }

        public async Task AddOrUpdateVersionDeploy(IDbConnection conn, VersionDeploy versionDeploy, IDbTransaction dbTranSaction = null)
        {
            if (versionDeploy == null)
                return;

            var cmd = _db.CreateCommand("select count(BatchID) from Version_Deploy where BatchID=@batchId", conn);
            if (dbTranSaction != null)
                cmd.Transaction = dbTranSaction;

            cmd.Parameters.Add(_db.CreateParameter("@batchId", versionDeploy.BatchId));
            cmd.Parameters.Add(_db.CreateParameter("@brandId", versionDeploy.BrandId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", versionDeploy.ProgramId));
            cmd.Parameters.Add(_db.CreateParameter("@programName", versionDeploy.ProgramName));
            cmd.Parameters.Add(_db.CreateParameter("@programVersion", versionDeploy.ProgramVersion));
            cmd.Parameters.Add(_db.CreateParameter("@fileUrl", versionDeploy.FileUrl));
            cmd.Parameters.Add(_db.CreateParameter("@batchStatus", versionDeploy.BatchStatus));
            cmd.Parameters.Add(_db.CreateParameter("@autoBackup", versionDeploy.AutoBackup));
            cmd.Parameters.Add(_db.CreateParameter("@scheduleUpdate", versionDeploy.ScheduleUpdate.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@insertDate", versionDeploy.InsertDate.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@updateDate", versionDeploy.UpdateDate.MinValueToDBNull()));

            var isHaveRecord = false;
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                isHaveRecord = reader.Read() && reader.GetInt32(0) > 0;
            }

            if (isHaveRecord)
            {
                cmd.CommandText = "update Version_Deploy set BatchID=@batchId, BrandID=@brandId, ProgramID=@programId," +
                    "ProgramName=@programName, ProgramVersion=@programVersion, FileUrl=@fileUrl, BatchStatus=@batchStatus, AutoBackup=@autoBackup, ScheduleUpdate=@scheduleUpdate," +
                    "InsertDate=@insertDate, UpdateDate=@updateDate where BatchID=@batchId";
            }
            else
            {
                cmd.CommandText = "insert into Version_Deploy(BatchID, BrandID, ProgramID, ProgramName, ProgramVersion, FileUrl, BatchStatus, AutoBackup," +
                    "ScheduleUpdate, InsertDate, UpdateDate) values (@batchId, @brandId, @programId, @programName, @programVersion," +
                    "@fileUrl, @batchStatus, @autoBackup, @scheduleUpdate, @insertDate, @updateDate)";
            }
            await _db.ExecuteNonQueryAsync(cmd);
        }

        public async Task AddOrUpdateVersionLiveUpdate(IDbConnection conn, VersionLiveUpdate liveUpdate)
        {
            if (liveUpdate == null)
                return;

            var cmd = _db.CreateCommand("select count(BatchID) from Version_LiveUpdate where BatchId=@batchId and ShopID=@shopId and ComputerID=@computerId" +
                " and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", liveUpdate.ShopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", liveUpdate.ComputerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", liveUpdate.ProgramId));
            cmd.Parameters.Add(_db.CreateParameter("@updateVersion", liveUpdate.UpdateVersion));
            cmd.Parameters.Add(_db.CreateParameter("@fileReceiveStatus", liveUpdate.FileReceiveStatus));
            cmd.Parameters.Add(_db.CreateParameter("@downloadFilePath", liveUpdate.DownloadFilePath));
            cmd.Parameters.Add(_db.CreateParameter("@revStartTime", liveUpdate.RevStartTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@backupStatus", liveUpdate.BackupStatus));
            cmd.Parameters.Add(_db.CreateParameter("@backupFilePath", liveUpdate.BackupFilePath));
            cmd.Parameters.Add(_db.CreateParameter("@backupStartTime", liveUpdate.BackupStartTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@backupEndTime", liveUpdate.BackupEndTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@scheduleUpdate", liveUpdate.ScheduleUpdate.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@updateEndTime", liveUpdate.UpdateEndTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@rollbackStatus", liveUpdate.RollbackStatus));
            cmd.Parameters.Add(_db.CreateParameter("@updateStatus", liveUpdate.UpdateStatus));
            cmd.Parameters.Add(_db.CreateParameter("@syncStatus", liveUpdate.SyncStatus));
            cmd.Parameters.Add(_db.CreateParameter("@readyToUpdate", liveUpdate.FileReceiveStatus == FileReceiveStatus.Downloaded && liveUpdate.BackupStatus == BackupStatus.BackupFinish));
            cmd.Parameters.Add(_db.CreateParameter("@messageLog", liveUpdate.MessageLog ?? ""));
            cmd.Parameters.Add(_db.CreateParameter("@batchId", liveUpdate.BatchId));
            cmd.Parameters.Add(_db.CreateParameter("@programName", liveUpdate.ProgramName));
            cmd.Parameters.Add(_db.CreateParameter("@revEndTime", liveUpdate.RevEndTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@updateStartTime", liveUpdate.UpdateStartTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@insertDate", DateTime.Now.ToISODateTime()));
            cmd.Parameters.Add(_db.CreateParameter("@updateDate", DateTime.Now.ToISODateTime()));

            var isHaveRecord = false;
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                isHaveRecord = reader.Read() && reader.GetInt32(0) > 0;
            }

            if (isHaveRecord)
            {
                cmd.CommandText = "update Version_LiveUpdate set BatchID=@batchId, ProgramName=@programName, FileReceiveStatus=@fileReceiveStatus, " +
                    " DownloadFilePath=@downloadFilePath, RevStartTime=@revStartTime," +
                    " RevEndTime=@revEndTime, BackupStatus=@backupStatus, BackupFilePath=@backupFilePath, BackupStartTime=@backupStartTime," +
                    " BackupEndTime=@backupEndtime, ScheduleUpdate=@scheduleUpdate," +
                    " UpdateEndTime=@updateEndTime, RollbackStatus=@rollbackStatus, UpdateStatus=@updateStatus," +
                    " SyncStatus=@syncStatus, ReadyToUpdate=@readyToUpdate, MessageLog=@messageLog, UpdateDate=@updateDate" +
                    " where BatchId=@batchId and ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId";
            }
            else
            {
                cmd.CommandText = "insert into Version_LiveUpdate(BatchID, ShopID, ComputerID, ProgramID, ProgramName, UpdateVersion," +
                    " FileReceiveStatus, DownloadFilePath, RevStartTime, RevEndTime, BackupStatus, BackupFilePath, BackupStartTime, BackupEndTime, ScheduleUpdate," +
                    " UpdateStartTime, UpdateEndTime, RollbackStatus, UpdateStatus, SyncStatus, ReadyToUpdate, MessageLog, InsertDate, UpdateDate)" +
                    " values (@batchId, @shopId, @computerId, @programId, @programName, @updateVersion, @fileReceiveStatus, @downloadFilePath, @revStartTime," +
                    " @revEndTime, @backupStatus, @backupFilePath, @backupStartTime, @backupEndTime, @scheduleUpdate, @updateStartTime," +
                    " @updateEndTime, @rollbackStatus, @updateStatus, @syncStatus, @readyToUpdate, @messageLog, @insertDate, @updateDate)";
            }
            await _db.ExecuteNonQueryAsync(cmd);
        }

        public async Task AddOrUpdateVersionInfo(IDbConnection conn, VersionInfo info)
        {
            if (info == null)
                return;

            var cmd = _db.CreateCommand("select count(ProgramID) from VersionInfo where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", info.ShopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", info.ComputerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", info.ProgramId));
            cmd.Parameters.Add(_db.CreateParameter("@programVersion", info.ProgramVersion));
            cmd.Parameters.Add(_db.CreateParameter("@versionStatus", info.VersionStatus));
            cmd.Parameters.Add(_db.CreateParameter("@syncStatus", info.SyncStatus));
            cmd.Parameters.Add(_db.CreateParameter("@programName", info.ProgramName));
            cmd.Parameters.Add(_db.CreateParameter("@connectionId", info.ConnectionId));
            cmd.Parameters.Add(_db.CreateParameter("@insertDate", DateTime.Now.ToISODateTime()));
            cmd.Parameters.Add(_db.CreateParameter("@updateDate", DateTime.Now.ToISODateTime()));

            var isHaveRecord = false;
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                isHaveRecord = reader.Read() && reader.GetInt32(0) > 0;
            }

            if (isHaveRecord)
            {
                cmd.CommandText = "update VersionInfo set ProgramVersion=@programVersion, VersionStatus=@versionStatus, UpdateDate=@updateDate, SyncStatus=@syncStatus," +
                    " ConnectionId=@connectionId where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId";
            }
            else
            {
                cmd.CommandText = "insert into VersionInfo(ShopID, ComputerID, ProgramID, ProgramName, ProgramVersion, VersionStatus, InsertDate, UpdateDate, SyncStatus, ConnectionId)" +
                    " values (@shopId, @computerId, @programId, @programName, @programVersion, @versionStatus, @insertDate, @updateDate, @syncStatus, @connectionId);";
            }
            await _db.ExecuteNonQueryAsync(cmd);
        }

        public async Task AddOrUpdateVersionLiveUpdateLog(IDbConnection conn, VersionLiveUpdateLog log)
        {
            if (log == null)
                return;

            var cmd = _db.CreateCommand("select count(LogUUID) from Version_LiveUpdateLog where LogUUID = @uuid", conn);
            cmd.Parameters.Add(_db.CreateParameter("@uuid", log.LogUUID));
            cmd.Parameters.Add(_db.CreateParameter("@saleDate", log.SaleDate));
            cmd.Parameters.Add(_db.CreateParameter("@shopId", log.ShopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", log.ComputerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", log.ProgramId));
            cmd.Parameters.Add(_db.CreateParameter("@actionId", log.ActionId));
            cmd.Parameters.Add(_db.CreateParameter("@programVersion", log.ProgramVersion));
            cmd.Parameters.Add(_db.CreateParameter("@actionStatus", log.ActionStatus));
            cmd.Parameters.Add(_db.CreateParameter("@startTime", log.StartTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@endTime", log.EndTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@logMessage", log.LogMessage ?? ""));

            var isHaveRecord = false;
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                isHaveRecord = reader.Read() && reader.GetInt32(0) > 0;
            }

            if (isHaveRecord)
            {
                cmd.CommandText = "update Version_LiveUpdateLog set SaleDate=@saleDate, ShopID=@shopId, ComputerID=@computerId, " +
                    "ProgramID=@programId, ActionID=@actionId, ProgramVersion=@programVersion, ActionStatus=@actionStatus, " +
                    "StartTime=@startTime, EndTime=@endTime, LogMessage=@logMessage where LogUUID=@uuid";
            }
            else
            {
                cmd.CommandText = "insert into Version_LiveUpdateLog(LogUUID, SaleDate, ShopID, ComputerID, ProgramID, ActionID, ProgramVersion, ActionStatus, StartTime, EndTime, LogMessage)" +
                    " values(@uuid, @saleDate, @shopId, @computerId, @programId, @actionId, @programVersion, @actionStatus, @startTime, @endTime, @logMessage)";
            }
            await _db.ExecuteNonQueryAsync(cmd);
        }

        public async Task<ProgramFileVersion> GetFileVersion(IDbConnection conn, int shopId, int computerId, string fileName)
        {
            var cmd = _db.CreateCommand("select * from fileversion where ShopID=@shopId and ComputerID=computerId and FileName=@fileName", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", computerId));
            cmd.Parameters.Add(_db.CreateParameter("@fileName", fileName));

            ProgramFileVersion fileVersion = null;
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    fileVersion = new ProgramFileVersion()
                    {
                        ShopId = reader.GetValue<int>("ShopID"),
                        ComputerId = reader.GetValue<int>("ComputerID"),
                        FileName = reader.GetValue<string>("FileName"),
                        FileVersion = reader.GetValue<string>("FileVersion"),
                        FileDate = reader.GetValue<DateTime>("FileDate"),
                        LastUpdateDate = reader.GetValue<DateTime>("LastUpdateDate")
                    };
                }
            }
            return fileVersion;
        }

        public async Task<VersionDeploy> GetActiveVersionDeploy(IDbConnection conn)
        {
            var versionsDeploy = await GetVersionDeploy(conn);
            return versionsDeploy.Where(v => v.BatchStatus == VersionDeployBatchStatus.Actived).FirstOrDefault();
        }
    }
}
