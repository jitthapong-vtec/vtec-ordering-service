using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Database;

namespace VerticalTec.POS.Service.DataSync.Owin.Utils
{
    public class Helper
    {
        public static async Task<bool> IsTableExists(IDatabase db, IDbConnection conn, string tableName)
        {
            var tableIsExists = false;
            var cmd = db.CreateCommand(conn);
            cmd.CommandText = "SELECT * FROM information_schema.tables WHERE table_schema = @dbName " +
                "AND TABLE_NAME = @tableName LIMIT 1; ";
            cmd.Parameters.Add(db.CreateParameter("@dbName", GlobalVar.Instance.DbName));
            cmd.Parameters.Add(db.CreateParameter("@tableName", tableName));
            using (var reader = await db.ExecuteReaderAsync(cmd))
            {
                if (reader.Read())
                {
                    tableIsExists = true;
                }
            }
            return tableIsExists;
        }
    }
}
