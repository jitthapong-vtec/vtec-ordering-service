using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync.Owin.Services
{
    public class FailureDataSyncRecovery : IFailureDataSyncRecovery
    {
        IDatabase _db;
        IDataSyncService _dataSyncService;
        POSModule _posModule;

        public FailureDataSyncRecovery(IDatabase db, IDataSyncService dataSyncService, POSModule posModule)
        {
            _db = db;
            _dataSyncService = dataSyncService;
            _posModule = posModule;
        }

        public async Task RecoveryInventoryDataSync()
        {
            using (var conn = await _db.ConnectAsync())
            {
                var dtLog = new DataTable();
                var cmd = _db.CreateCommand($"select * from {Constants.TAB_LOG_FAILURE_SYNC_INV} where IsCanceled=0", conn);
                using (var reader = await _db.ExecuteReaderAsync(cmd))
                {
                    dtLog.Load(reader);
                }

                if (dtLog.Rows.Count > 0)
                {
                    foreach (DataRow row in dtLog.Rows)
                    {
                        var startDate = string.Format(CultureInfo.InvariantCulture, "'{0:yyyy-MM-dd}'", row.GetValue<DateTime>("StartDate"));
                        var endDate = string.Format(CultureInfo.InvariantCulture, "'{0:yyyy-MM-dd}'", row.GetValue<DateTime>("EndDate"));
                        var exportType = row.GetValue<int>("ExportType");
                        var shopId = row.GetValue<int>("ShopId");

                        var respText = "";
                        _posModule.DocumentResetSync(ref respText, shopId, startDate, endDate, conn as MySqlConnection);
                        await _dataSyncService.SyncInvData(conn, shopId, startDate, endDate, exportType: exportType);
                    }
                }
                else
                {
                    cmd.CommandText = $"delete from {Constants.TAB_LOG_FAILURE_SYNC_INV} where IsCanceled=1 and InsertDate <= @outDate";
                    cmd.Parameters.Add(_db.CreateParameter("@outDate", DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                    await _db.ExecuteNonQueryAsync(cmd);
                }
            }
        }

        public Task RecoverySaleDataSync()
        {
            throw new NotImplementedException();
        }
    }
}
