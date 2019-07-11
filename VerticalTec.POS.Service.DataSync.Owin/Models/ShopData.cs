using System;
using System.Data;
using System.Threading.Tasks;
using VerticalTec.POS.Database;

namespace VerticalTec.POS.Service.DataSync.Owin.Models
{
    public class ShopData
    {
        IDatabase _database;

        public ShopData(IDatabase database)
        {
            _database = database;
        }

        public async Task<DataRow> GetShopDataAsync(IDbConnection conn, int shopId)
        {
            var dtShop = new DataTable();
            var cmd = _database.CreateCommand("select * from shop_data where ShopID=@shopId", conn);
            cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            using(var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtShop.Load(reader);
            }
            if (dtShop.Rows.Count == 0)
                throw new Exception($"Not found shop_data of shopId {shopId}");
            return dtShop.Rows[0];
        }
    }
}
