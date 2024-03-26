using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using VerticalTec.POS.Printer.Epson;
using VerticalTec.POS.Utils;
using vtecPOS.GlobalFunctions;
using vtecPOS.POSControl;

namespace VerticalTec.POS.Printer
{
    public class DatabaseManager
    {
        CDBUtil _dbUtil;
        public DatabaseManager()
        {
            _dbUtil = new CDBUtil();
        }

        internal void UpdateOrderPrintStatus(string batchUuid, int statusId,
                                             string errMessage, int transactionId,
                                             int computerId)
        {
            using (MySqlConnection conn = _dbUtil.EstablishConnection(EpsonPrintManager.Instance.DbAddress,
                EpsonPrintManager.Instance.DbName, EpsonPrintManager.Instance.DbPort))
            {
                var posModule = new POSModule();
                string responseText = "";
                bool isSuccess = posModule.Table_UpdatePrintStatus(ref responseText, batchUuid,
                                                  statusId, errMessage, "front",
                                                  transactionId, computerId,
                                                  EpsonPrintManager.Instance.ShopId,
                                                  EpsonPrintManager.Instance.SaleDate,
                                                  EpsonPrintManager.Instance.LangId, conn);
                if (!isSuccess)
                    throw new EpsonPrintException(responseText);
            }
        }

        internal DataTable GetPrinter(string printerIds = "")
        {
            using (MySqlConnection conn = _dbUtil.EstablishConnection(EpsonPrintManager.Instance.DbAddress,
                   EpsonPrintManager.Instance.DbName, EpsonPrintManager.Instance.DbPort))
            {
                var cmd = new MySqlCommand("", conn);
                string sqlQuery = "select * from printers where Deleted=0";
                cmd.CommandText = sqlQuery;
                if (!string.IsNullOrEmpty(printerIds))
                {
                    sqlQuery += " and PrinterID in (" + printerIds + ")";
                    cmd.CommandText = sqlQuery;
                }
                IDataReader reader = cmd.ExecuteReader();
                DataTable dataTable = new DataTable();
                dataTable.Load(reader);
                return dataTable;
            }
        }

        internal int GetDefaultDecimalDigit()
        {
            int decimalDigit = 0;
            try
            {
                var deimalDigit = GetPropertyValue(24, "PropertyValue");
                decimalDigit = Convert.ToInt32(decimalDigit);
            }
            catch (Exception) { }
            return decimalDigit;
        }

        public string GetPropertyValue(int propertyId, string param, int shopId = 0, int computerId = 0)
        {
            using (MySqlConnection conn = _dbUtil.EstablishConnection(EpsonPrintManager.Instance.DbAddress,
                   EpsonPrintManager.Instance.DbName, EpsonPrintManager.Instance.DbPort))
            {
                var dtProp = GetProgramProperty(conn, propertyId);
                if (dtProp.Rows.Count == 0)
                    return "";
                var propRow = dtProp.Rows[0];
                if (dtProp.Rows.Count > 1)
                {
                    var keyId = 0;
                    var propLevel = propRow.GetValue<int>("PropertyLevelID");

                    if (propLevel == 1)
                        keyId = shopId;
                    else if (propLevel == 2)
                        keyId = computerId;

                    var propLevelShop = dtProp.AsEnumerable().Where(row => row.GetValue<int>("KeyID") == keyId).FirstOrDefault();
                    if (propLevelShop != null)
                        propRow = propLevelShop;
                }
                var dict = ExtractPropertyParameter(propRow.GetValue<string>("PropertyTextValue"));
                var val = dict.FirstOrDefault(x => x.Key == param).Value;
                return val;
            }
        }

        public DataTable GetProgramProperty(MySqlConnection conn, int propertyId)
        {
            string sqlQuery = "select a.*, b.PropertyLevelID from programpropertyvalue a" +
                " left join programproperty b" +
                " on a.PropertyID=b.PropertyID" +
                " where a.PropertyID=@propertyId";
            MySqlCommand cmd = new MySqlCommand(sqlQuery, conn);
            cmd.Parameters.Add(new MySqlParameter("@propertyId", propertyId));
            DataTable dtResult = new DataTable();
            using (IDataReader reader = cmd.ExecuteReader())
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        Dictionary<string, string> ExtractPropertyParameter(string propParams)
        {
            var props = propParams.Split(';').AsParallel().Select(x => x.Split('=')).ToArray();
            var dict = new Dictionary<string, string>();
            foreach (var prop in props)
            {
                try
                {
                    if (!dict.Keys.Contains(prop[0]))
                        dict.Add(prop[0], prop[1]);
                }
                catch (Exception) { }
            }
            return dict;
        }
    }
}
