using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdateConsole.Models;

namespace VerticalTec.POS.LiveUpdateConsole.Services
{
    public class RepoService
    {
        IDatabase _db;

        public RepoService(IDatabase db)
        {
            _db = db;
        }

        public async Task<List<BrandData>> GetBrandAsync()
        {
            List<BrandData> brands = new List<BrandData>();
            using (var conn = await _db.ConnectAsync())
            {
                var cmd = _db.CreateCommand(conn);
                cmd.CommandText = "select a.BrandID, a.BrandName from Brand_Data a inner join Shop_Data b on a.BrandID = b.BrandID where a.Deleted = 0 and IsShop = 1 group by a.BrandID, a.BrandName";
                using (var reader = await _db.ExecuteReaderAsync(cmd))
                {
                    while (reader.Read())
                    {
                        brands.Add(new BrandData()
                        {
                            BrandId = reader.GetValue<int>("BrandID"),
                            BrandName = reader.GetValue<string>("BrandName")
                        });
                    }
                }
            }
            return brands;
        }

        public async Task<List<ShopData>> GetShopSelectedUpdateAsync(string batchId)
        {
            List<ShopData> shops = new List<ShopData>();
            using (var conn = await _db.ConnectAsync())
            {
                var cmd = _db.CreateCommand(conn);
                cmd.CommandText = "select a.ShopID, a.BrandID, a.ShopCatID1, a.ShopCode, a.ShopName, case when b.ShopID is null then 0 else 1 end as Selected" +
                        " from shop_data a left join " +
                        " (select ShopID from Version_LiveUpdate where BatchID=@batchId group by ShopID) b" +
                        " on a.ShopID=b.ShopID" +
                        " where a.Deleted=0 and a.IsShop=1 and a.MasterShop = 0";
                cmd.Parameters.Add(_db.CreateParameter("@batchId", batchId));
                using (var reader = await _db.ExecuteReaderAsync(cmd))
                {
                    while (reader.Read())
                    {
                        shops.Add(new ShopData()
                        {
                            ShopId = reader.GetValue<int>("ShopID"),
                            ShopCode = reader.GetValue<string>("ShopCode"),
                            ShopName = reader.GetValue<string>("ShopName"),
                            BrandId = reader.GetValue<int>("BrandID"),
                            ShopCatId = reader.GetValue<int>("ShopCatID1"),
                            Selected = reader.GetValue<bool>("Selected")
                        });
                    }
                }
            }
            return shops;
        }

        public async Task<List<ShopCategory>> GetShopCategoryAsync()
        {
            List<ShopCategory> items = new List<ShopCategory>();
            items.Add(new ShopCategory()
            {
                ShopCateId = 0,
                ShopCateName = "-- All Shop Type--"
            }); ;
            using (var conn = await _db.ConnectAsync())
            {
                var cmd = _db.CreateCommand(conn);
                cmd.CommandText = "select * from shop_category where Deleted=0 order by ShopCatOrdering";
                using(var reader = await _db.ExecuteReaderAsync(cmd))
                {
                    while (reader.Read())
                    {
                        items.Add(new ShopCategory()
                        {
                            ShopCateId = reader.GetValue<int>("ShopCatID"),
                            ShopCateCode = reader.GetValue<string>("ShopCatCode"),
                            ShopCateName = reader.GetValue<string>("ShopCatName"),
                        });
                    }
                }
            }
                return items;
        }

        public async Task<List<ShopData>> GetShopAsync(int brandId = 0, int shopCatId=0)
        {
            List<ShopData> shops = new List<ShopData>();
            using (var conn = await _db.ConnectAsync())
            {
                var cmd = _db.CreateCommand(conn);
                cmd.CommandText = "select ShopID, BrandID, ShopName from shop_data " +
                        " where Deleted=0 and IsShop=1 and MasterShop = 0 ";

                if(brandId > 0)
                {
                    cmd.CommandText += " and BrandID=@brandId";
                    cmd.Parameters.Add(_db.CreateParameter("@brandId", brandId));
                }

                if(shopCatId > 0)
                {
                    cmd.CommandText += " and ShopCatID1=@shopCatId";
                    cmd.Parameters.Add(_db.CreateParameter("@shopCatId", shopCatId));
                }

                using (var reader = await _db.ExecuteReaderAsync(cmd))
                {
                    while (reader.Read())
                    {
                        shops.Add(new ShopData()
                        {
                            ShopId = reader.GetValue<int>("ShopID"),
                            ShopName = reader.GetValue<string>("ShopName"),
                            BrandId = reader.GetValue<int>("BrandID")
                        });
                    }
                }
            }
            return shops;
        }
    }
}
