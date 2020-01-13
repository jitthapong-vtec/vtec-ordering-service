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
               ShopID INT NOT NULL,
               ProgramID SMALLINT NOT NULL,
               ProgramName VARCHAR(100) NOT NULL,
               ProgramVersion VARCHAR(20) NOT NULL,
               FileID VARCHAR(50),
               BatchStatus TINYINT NOT NULL,
               ScheduleUpdate DATETIME NULL,
               InsertDate DATETIME NOT NULL,
               UpdateDate DATETIME NOT NULL,
               PRIMARY KEY(ShopID, ProgramID, ProgramVersion)
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
               RevFile TINYINT NOT NULL,
               RevStartTime DATETIME NULL,
               RevEndTime DATETIME NULL,
               BackupStatus TINYINT NOT NULL,
               BackupStartTime DATETIME NULL,
               BackupEndTime DATETIME NULL,
               ScheduleUpdate DATETIME NULL,
               UpdateStartTime DATETIME NULL,
               UpdateEndTime DATETIME NULL,
               RollbackStatus TINYINT NOT NULL DEFAULT '0',
               UpdateStatus TINYINT NOT NULL DEFAULT '0',
               SyncStatus TINYINT NOT NULL DEFAULT '0',
               MessageLog VARCHAR(2000) NULL,
               InsertDate DATETIME NOT NULL,
               UpdateDate DATETIME NOT NULL,
               PRIMARY KEY(ShopID, ComputerID, ProgramID, UpdateVersion)
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
            );"};

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

        public async Task<DataTable> GetVersionComputer(IDbConnection conn)
        {
            DataTable dt = new DataTable();
            var cmd = _db.CreateCommand(@"SELECT a.*, b.ShopName, c.ComputerName
                FROM versioninfo a
                LEFT JOIN shop_data b
                ON a.ShopID=b.ShopID
                LEFT JOIN computername c
                ON a.ComputerID=c.ComputerID AND a.ShopID=c.ShopID", conn);
            using(var reader = await _db.ExecuteReaderAsync(cmd))
            {
                dt.Load(reader);
            }
            return dt;
        }

        public async Task<VersionLiveUpdate> GetVersionLiveUpdate(IDbConnection conn, int shopId, int computerId, int programId = 0)
        {
            var cmd = _db.CreateCommand("select * from version_liveupdate where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", computerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", programId));

            VersionLiveUpdate versionLiveUpdate = null;
            using(var reader = await _db.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    versionLiveUpdate = new VersionLiveUpdate()
                    {
                        BatchId = reader.GetValue<string>("BatchID"),
                        ShopId = reader.GetValue<int>("ShopID"),
                        ComputerId = reader.GetValue<int>("ComputerID"),
                        ProgramId = reader.GetValue<int>("ProgramID"),
                        ProgramName = reader.GetValue<string>("ProgramName"),
                        UpdateVersion = reader.GetValue<string>("UpdateVersion"),
                        RevFile = reader.GetValue<int>("RevFile"),
                        RevStartTime = reader.GetValue<DateTime>("RevStartTime"),
                        RevEndTime = reader.GetValue<DateTime>("RevEndTime"),
                        BackupStatus = reader.GetValue<int>("BackupStatus"),
                        BackupStartTime = reader.GetValue<DateTime>("BackupStartTime"),
                        BackupEndTime = reader.GetValue<DateTime>("BackupEndTime"),
                        ScheduleUpdate = reader.GetValue<DateTime>("ScheduleUpdate"),
                        SyncStatus = reader.GetValue<int>("SyncStatus"),
                        MessageLog = reader.GetValue<string>("MessageLog"),
                        InsertDate = reader.GetValue<DateTime>("InsertDate"),
                        UpdateDate = reader.GetValue<DateTime>("UpdateDate")
                    };
                }
            }
            return versionLiveUpdate;
        }

        public async Task<VersionLiveUpdateLog> GetVersionLiveUpdateLog(IDbConnection conn, int shopId, int computerId, int programId = 0)
        {
            var cmd = _db.CreateCommand("select * from Version_LiveUpdateLog where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", computerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", programId));

            VersionLiveUpdateLog log = null;
            using(var reader = await _db.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    log = new VersionLiveUpdateLog()
                    {
                        LogUUID = reader.GetValue<string>("LogUUID"),
                        SaleDate = reader.GetValue<DateTime>("SaleDate"),
                        ShopId = reader.GetValue<int>("ShopID"),
                        ComputerId = reader.GetValue<int>("ComputerID"),
                        ProgramId = reader.GetValue<int>("ProgramID"),
                        ActionId = reader.GetValue<int>("ActionID"),
                        ProgramVersion = reader.GetValue<string>("ProgramVersion"),
                        ActionStatus = reader.GetValue<int>("ActionStatus"),
                        StartTime = reader.GetValue<DateTime>("StartTime"),
                        EndTime = reader.GetValue<DateTime>("EndTime"),
                        LogMessage = reader.GetValue<string>("LogMessage")
                    };
                }
            }
            return log;
        }

        public async Task<VersionDeploy> GetVersionDeploy(IDbConnection conn, int shopId, int programId = 0)
        {
            var cmd = _db.CreateCommand("select * from Version_Deploy where ShopID=@shopId and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", programId));

            VersionDeploy versionDeploy = null;
            using(var reader = await _db.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    versionDeploy = new VersionDeploy()
                    {
                        BatchId = reader.GetValue<string>("BatchID"),
                        BrandId = reader.GetValue<int>("BrandID"),
                        ShopId = reader.GetValue<int>("ShopID"),
                        ProgramId = reader.GetValue<int>("ProgramID"),
                        ProgramName = reader.GetValue<string>("ProgramName"),
                        ProgramVersion = reader.GetValue<string>("ProgramVersion"),
                        FileId = reader.GetValue<string>("FileId"),
                        BatchStatus = reader.GetValue<int>("BatchStatus"),
                        ScheduleUpdate = reader.GetValue<DateTime>("ScheduleUpdate"),
                        InsertDate = reader.GetValue<DateTime>("InsertDate"),
                        UpdateDate = reader.GetValue<DateTime>("UpdateDate")
                    };
                }
            }
            return versionDeploy;
        }

        public async Task<VersionInfo> GetVersionInfo(IDbConnection conn, int shopId, int computerId, int programId = 0)
        {
            var cmd = _db.CreateCommand("select * from VersionInfo where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", shopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", computerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", programId));

            VersionInfo versionInfo = null;
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    versionInfo = new VersionInfo()
                    {
                        ShopId = shopId,
                        ComputerId = computerId,
                        ProgramId = reader.GetValue<int>("ProgramID"),
                        ConnectionId = reader.GetValue<string>("ConnectionId"),
                        ProgramName = reader.GetValue<string>("ProgramName"),
                        ProgramVersion = reader.GetValue<string>("ProgramVersion"),
                        VersionStatus = reader.GetValue<int>("VersionStatus"),
                        InsertDate = reader.GetValue<DateTime>("InsertDate"),
                        UpdateDate = reader.GetValue<DateTime>("UpdateDate"),
                        SyncStatus = reader.GetValue<int>("SyncStatus")
                    };
                }
            }
            return versionInfo;
        }

        public async Task AddOrUpdateVersionDeploy(IDbConnection conn, VersionDeploy versionDeploy)
        {
            if (versionDeploy == null)
                return;

            var cmd = _db.CreateCommand("select count(BatchID) from Version_Deploy where ShopID=@shopId and ProgramID=@programId and ProgramVersion=@programVersion", conn);
            cmd.Parameters.Add(_db.CreateParameter("@batchId", versionDeploy.BatchId));
            cmd.Parameters.Add(_db.CreateParameter("@brandId", versionDeploy.BrandId));
            cmd.Parameters.Add(_db.CreateParameter("@shopId", versionDeploy.ShopId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", versionDeploy.ProgramId));
            cmd.Parameters.Add(_db.CreateParameter("@programName", versionDeploy.ProgramName));
            cmd.Parameters.Add(_db.CreateParameter("@programVersion", versionDeploy.ProgramVersion));
            cmd.Parameters.Add(_db.CreateParameter("@fileId", versionDeploy.FileId));
            cmd.Parameters.Add(_db.CreateParameter("@batchStatus", versionDeploy.BatchStatus));
            cmd.Parameters.Add(_db.CreateParameter("@scheduleUpdate", versionDeploy.ScheduleUpdate.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@insertDate", versionDeploy.InsertDate.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@updateDate", versionDeploy.UpdateDate.MinValueToDBNull()));

            var isHaveRecord = false;
            using(var reader = await _db.ExecuteReaderAsync(cmd))
            {
                isHaveRecord = reader.Read() && reader.GetInt32(0) > 0;
            }

            if (isHaveRecord)
            {
                cmd.CommandText = "update Version_Deploy set BatchID=@batchId, BrandID=@brandId, ShopID=@shopId, ProgramID=@programId," +
                    "ProgramName=@programName, ProgramVersion=@programVersion, FileID=@fileId, BatchStatus=@batchStatus, ScheduleUpdate=@scheduleUpdate," +
                    "InsertDate=@insertDate, UpdateDate=@updateDate where ShopID=@shopId and ProgramID=@programId and ProgramVersion=@programVersion";
            }
            else
            {
                cmd.CommandText = "insert into Version_Deploy(BatchID, BrandID, ShopID, ProgramID, ProgramName, ProgramVersion, FileID, BatchStatus," +
                    "ScheduleUpdate, InsertDate, UpdateDate) values (@batchId, @brandId, @shopId, @programId, @programName, @programVersion," +
                    "@fileId, @batchStatus, @scheduleUpate, @insertDate, @updateDate)";
            }
            await _db.ExecuteNonQueryAsync(cmd);
        }

        public async Task AddOrUpdateVersionLiveUpdate(IDbConnection conn, VersionLiveUpdate liveUpdate)
        {
            if (liveUpdate == null)
                return;

            var cmd = _db.CreateCommand("select count(BatchID) from Version_LiveUpdate where ShopID=@shopId and ComputerID=@computerId" +
                " and ProgramID=@programId and UpdateVersion=@updateVersion", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", liveUpdate.ShopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", liveUpdate.ComputerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", liveUpdate.ProgramId));
            cmd.Parameters.Add(_db.CreateParameter("@updateVersion", liveUpdate.UpdateVersion));
            cmd.Parameters.Add(_db.CreateParameter("@revFile", liveUpdate.RevFile));
            cmd.Parameters.Add(_db.CreateParameter("@revStartTime", liveUpdate.RevStartTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@backupStatus", liveUpdate.BackupStatus));
            cmd.Parameters.Add(_db.CreateParameter("@backupStartTime", liveUpdate.BackupStartTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@backupEndTime", liveUpdate.BackupEndTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@scheduleUpdate", liveUpdate.ScheduleUpdate.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@updateEndTime", liveUpdate.UpdateEndTime.MinValueToDBNull()));
            cmd.Parameters.Add(_db.CreateParameter("@rollbackStatus", liveUpdate.RollbackStatus));
            cmd.Parameters.Add(_db.CreateParameter("@updateStatus", liveUpdate.UpdateStatus));
            cmd.Parameters.Add(_db.CreateParameter("@syncStatus", liveUpdate.SyncStatus));
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
                cmd.CommandText = "update Version_LiveUpdate set RevFile=@revFile, RevStartTime=@revStartTime," +
                    " RevEndTime=@revEndTime, BackupStatus=@backupStatus, BackupStartTime=@backupStartTime," +
                    " BackupEndTime=@backupEndtime, ScheduleUpdate=@scheduleUpdate," +
                    " UpdateEndTime=@updateEndTime, RollbackStatus=@rollbackStatus, UpdateStatus=@updateStatus," +
                    " SyncStatus=@syncStatus, MessageLog=@messageLog, UpdateDate=@updateDate" +
                    " where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId and UpdateVersion=@updateVersion";
            }
            else
            {
                cmd.CommandText = "insert into Version_LiveUpdate(BatchID, ShopID, ComputerID, ProgramID, ProgramName, UpdateVersion," +
                    " RevFile, RevStartTime, RevEndTime, BackupStatus, BackupStartTime, BackupEndTime, ScheduleUpdate," +
                    " UpdateStartTime, UpdateEndTime, RollbackStatus, UpdateStatus, SyncStatus, MessageLog, InsertDate, UpdateDate)" +
                    " values (@batchId, @shopId, @computerId, @programId, @programName, @updateVersion, @revFile, @revStartTime," +
                    " @revEndTime, @backupStatus, @backupStartTime, @backupEndTime, @scheduleUpdate, @updateStartTime," +
                    " @updateEndTime, @rollbackStatus, @updateStatus, @syncStatus, @messageLog, @insertDate, @updateDate)";
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
    }
}
