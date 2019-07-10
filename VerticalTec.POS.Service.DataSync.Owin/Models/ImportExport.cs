using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vtecPOS.GlobalFunctions;

namespace VerticalTec.POS.Service.DataSync.Owin.Models
{
    public class ImportExport
    {
        POSModule _posModule;

        public ImportExport(POSModule posModule)
        {
            _posModule = posModule;
        }

        public Task UpdateSyncInventAsync(MySqlConnection conn, string syncJson)
        {
            var respText = "";
            var success = _posModule.SyncInventUpdate(ref respText, syncJson, conn);
            if (!success)
                throw new Exception(respText);
            return Task.FromResult(true);
        }

        public Task<string> ExportInvAsync(MySqlConnection conn, string docDate, int shopId, int documentId = 0, int keyShopId = 0)
        {
            var respText = "";
            var json = "";
            var dataSet = new DataSet();
            var exportType = 100;
            var merchantId = 0;
            var brandId = 0;

            var success = _posModule.ExportInventData(ref respText, ref dataSet, ref json, exportType, docDate, shopId,
                documentId, keyShopId, merchantId, brandId, conn);
            if (!success)
                throw new Exception(respText);
            return Task.FromResult(json);
        }
    }
}
