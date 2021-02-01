using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;
using VerticalTec.POS.LiveUpdateConsole.Models;
using VerticalTec.POS.LiveUpdateConsole.Services;

namespace VerticalTec.POS.LiveUpdateConsole.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    public class BranchController : ControllerBase
    {
        private IDatabase _db;
        private RepoService _repoService;
        private LiveUpdateDbContext _liveUpdateCtx;

        public BranchController(IDatabase db, LiveUpdateDbContext ctx, RepoService repo)
        {
            _db = db;
            _liveUpdateCtx = ctx;
            _repoService = repo;
        }

        [HttpGet]
        public async Task<object> GetBrandsAsync(DataSourceLoadOptions options)
        {
            List<BrandData> brands = await _repoService.GetBrandAsync();
            return DataSourceLoader.Load(brands, options);
        }

        [HttpGet]
        public async Task<object> GetShopsAsync(DataSourceLoadOptions options, string batchId, int brandId=0, int shopCatId=0)
        {
            List<ShopData> shops = await _repoService.GetShopSelectedUpdateAsync(batchId);
            if (brandId > 0)
                shops = shops.Where(s => s.BrandId == brandId).ToList();
            if (shopCatId > 0)
                shops = shops.Where(s => s.ShopCateId == shopCatId).ToList();

            return DataSourceLoader.Load(shops, options);
        }

        [HttpGet]
        public async Task<object> GetShopsCatAsync(DataSourceLoadOptions options)
        {
            List<ShopCategory> shopCat = await _repoService.GetShopCategoryAsync();
            return DataSourceLoader.Load(shopCat, options);
        }
    }
}
