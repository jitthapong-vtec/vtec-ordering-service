using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.POS.Database;

namespace VerticalTec.POS.Service.Ordering.Owin
{
    public static class DatabaseMigration
    {
        public static void CheckAndUpdate(IDatabase db, string dbName)
        {
            Task.Run(async () =>
            {
                using (var conn = await db.ConnectAsync())
                {
                    var cmd = db.CreateCommand("SELECT * FROM INFORMATION_SCHEMA.COLUMNS " +
                        "WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = @tableName " +
                        "AND COLUMN_NAME=@columnName", conn);
                    cmd.Parameters.Add(db.CreateParameter("@dbName", dbName));
                    cmd.Parameters.Add(db.CreateParameter("@tableName", "kiosk_page"));
                    cmd.Parameters.Add(db.CreateParameter("@columnName", "IsSuggestion"));

                    var isExists = false;
                    using (var reader = await db.ExecuteReaderAsync(cmd))
                    {
                        if (reader.Read())
                            isExists = true;
                    }

                    if (!isExists)
                    {
                        cmd.CommandText = "ALTER TABLE kiosk_page ADD IsSuggestion TINYINT NOT NULL DEFAULT 0";
                        cmd.Parameters.Clear();
                        await db.ExecuteNonQueryAsync(cmd);
                    }
                }
            });
        }
    }
}
