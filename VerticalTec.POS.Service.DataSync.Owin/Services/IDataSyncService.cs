using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.DataSync.Owin.Services
{
    public interface IDataSyncService
    {
        Task SyncInvData(IDbConnection conn, int shopId, string startDate, string endDate, string batchUuid = "", int exportType = 0);

        Task SyncSaleData();
    }
}
