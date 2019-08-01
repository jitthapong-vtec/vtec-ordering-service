using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using VerticalTec.POS.Database;
using VerticalTec.POS.Service.DataSync.Owin.Utils;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.Service.DataSync.Owin.Models
{
    public class ProgramProperty
    {
        IDatabase _database;
        
        public ProgramProperty(IDatabase database)
        {
            _database = database;
        }

        public string GetVdsUrl(IDbConnection conn)
        {
            return UriUtils.ValidateUriFormat(GetPropertyValue(conn, 1050, "vdsurl"));
        }

        public string GetCommissionApiUrl(IDbConnection conn)
        {
            return UriUtils.ValidateUriFormat(GetPropertyValue(conn, 2003, "CommissionApi"));
        }

        public string GetPropertyValue(IDbConnection conn, int propertyId, string param, int shopId = 0, int computerId = 0)
        {
            var dtProp = GetProgramProperty(conn, propertyId);
            if (dtProp.Rows.Count == 0)
                throw new Exception($"No property {propertyId}");
            var propRow = dtProp.Rows[0];
            if (propRow.GetValue<int>("PropertyValue") == 0)
                throw new Exception($"Property {propertyId} is disabled");
            if (dtProp.Rows.Count > 1)
            {
                var keyId = 0;
                var propLevel = propRow.GetValue<int>("PropertyLevelID");

                if (propLevel == 1)
                    keyId = shopId;
                else if (propLevel == 2)
                    keyId = computerId;

                var propLevelShop = dtProp.Select($"KeyID = {keyId}").FirstOrDefault();
                    //dtProp.AsEnumerable().Where(row => row.GetValue<int>("KeyID") == keyId).FirstOrDefault();
                if (propLevelShop != null)
                    propRow = propLevelShop;
            }
            var dict = ExtractPropertyParameter(propRow.GetValue<string>("PropertyTextValue"));
            var val = dict.FirstOrDefault(x => x.Key == param).Value;
            return val;
        }

        public DataTable GetProgramProperty(IDbConnection conn, int propertyId)
        {
            string sqlQuery = "select a.*, b.PropertyLevelID from programpropertyvalue a" +
                " left join programproperty b" +
                " on a.PropertyID=b.PropertyID" +
                " where a.PropertyID=@propertyId";
            IDbCommand cmd = _database.CreateCommand(sqlQuery, conn);
            cmd.Parameters.Add(_database.CreateParameter("@propertyId", propertyId));
            DataTable dtResult = new DataTable();
            using (IDataReader reader = cmd.ExecuteReader())
            {
                dtResult.Load(reader);
            }
            return dtResult;
        }

        public Dictionary<string, string> ExtractPropertyParameter(string propParams)
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
