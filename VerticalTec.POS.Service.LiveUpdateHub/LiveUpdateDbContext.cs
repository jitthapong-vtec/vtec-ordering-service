using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.Share.LiveUpdate;

namespace VerticalTec.POS.Service.LiveUpdateHub
{
    public class LiveUpdateDbContext
    {
        IDatabase _db;

        public LiveUpdateDbContext(IDatabase db)
        {
            _db = db;
        }

        public async Task AddOrUpdateVersionInfo(IDbConnection conn, VersionInfo info)
        {
            var cmd = _db.CreateCommand("select count(ProgramID) from VersionInfo where ShopID=@shopId and ComputerID=@computerId and ProgramID=@programId", conn);
            cmd.Parameters.Add(_db.CreateParameter("@shopId", info.ShopId));
            cmd.Parameters.Add(_db.CreateParameter("@computerId", info.ComputerId)); 
            cmd.Parameters.Add(_db.CreateParameter("@programId", info.ProgramId));

            var isHaveRecord = false;
            using(var reader = await _db.ExecuteReaderAsync(cmd))
            {
                isHaveRecord = reader.Read() && reader.GetInt32(0) > 0;
            }

            if (!isHaveRecord)
            {

            }
            else
            {

            }
        }
    }
}
