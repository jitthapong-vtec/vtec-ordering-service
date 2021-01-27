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
        public async Task<object> GetShopsAsync(DataSourceLoadOptions options, int brandId)
        {
            List<ShopData> shops = await _repoService.GetShopAsync(brandId: brandId);
            return DataSourceLoader.Load(shops, options);
        }
    }
}
