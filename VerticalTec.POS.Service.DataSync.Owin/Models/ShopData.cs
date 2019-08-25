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

        public async Task<DataTable> GetShopDataAsync(IDbConnection conn, int shopId = 0)
        {
            var dtShop = new DataTable();
            var cmd = _database.CreateCommand("select * from shop_data where Deleted=0", conn);
            if (shopId > 0)
            {
                cmd.CommandText += " and ShopID=@shopId";
                cmd.Parameters.Add(_database.CreateParameter("@shopId", shopId));
            }
            using(var reader = await _database.ExecuteReaderAsync(cmd))
            {
                dtShop.Load(reader);
            }
            return dtShop;
        }
    }
}
