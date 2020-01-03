using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Share.LiveUpdate
{
    public class LiveUpdateDbContext
    {
        IDatabase _db;

        public LiveUpdateDbContext(IDatabase db)
        {
            _db = db;
        }

        public async Task AddOrUpdateVersionLiveUpdate(IDbConnection conn, VersionLiveUpdate liveUpdate)
        {
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
            cmd.Parameters.Add(_db.CreateParameter("@messageLog", liveUpdate.MessageLog));
            cmd.Parameters.Add(_db.CreateParameter("@updateDate", DateTime.Now.ToISODateTime()));
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
            var cmd = _db.CreateCommand("select count(ProgramID) from VersionInfo where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", info.ShopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", info.ComputerId));
            cmd.Parameters.Add(_db.CreateParameter("@programId", info.ProgramId));
            cmd.Parameters.Add(_db.CreateParameter("@programVersion", info.ProgramVersion));
            cmd.Parameters.Add(_db.CreateParameter("@versionStatus", info.VersionStatus));
            cmd.Parameters.Add(_db.CreateParameter("@syncStatus", info.SyncStatus));
            cmd.Parameters.Add(_db.CreateParameter("@programName", info.ProgramName));
            cmd.Parameters.Add(_db.CreateParameter("@insertDate", DateTime.Now.ToISODateTime()));
            cmd.Parameters.Add(_db.CreateParameter("@updateDate", DateTime.Now.ToISODateTime()));

            var isHaveRecord = false;
            using (var reader = await _db.ExecuteReaderAsync(cmd))
            {
                isHaveRecord = reader.Read() && reader.GetInt32(0) > 0;
            }

            if (isHaveRecord)
            {
                cmd.CommandText = "update VersionInfo set ProgramVersion=@programVersion, VersionStatus=@versionStatus, UpdateDate=@updateDate, SyncStatus=@syncStatus" +
                    " where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId";
            }
            else
            {
                cmd.CommandText = "insert into VersionInfo(ShopID, ComputerID, ProgramID, ProgramName, ProgramVersion, VersionStatus, InsertDate, UpdateDate, SyncStatus)" +
                    " values (@shopId, @computerId, @programId, @programName, @programVersion, @versionStatus, @insertDate, @updateDate, @syncStatus);";
            }
            await _db.ExecuteNonQueryAsync(cmd);
        }

        public async Task AddOrUpdateVersionLiveUpdateLog(IDbConnection conn, VersionLiveUpdateLog log)
        {
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
            cmd.Parameters.Add(_db.CreateParameter("@logMessage", log.LogMessage));

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
    }
}
