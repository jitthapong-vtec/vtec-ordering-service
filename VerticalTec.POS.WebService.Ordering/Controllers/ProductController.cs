using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VerticalTec.POS.Database;
using VerticalTec.POS.Utils;

namespace VerticalTec.POS.WebService.Ordering.Controllers
{
    [Route("products")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        IDatabase _db;
        VtecRepo _vtRepo;

        public ProductController(IDatabase db)
        {
            _db = db;
            _vtRepo = new VtecRepo(db);
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetProducts(int shopId, SaleModes saleMode = SaleModes.DineIn)
        {
            using (var conn = await _db.ConnectAsync())
            {
                return await _vtRepo.GetProductsAsync(conn, shopId, saleMode);
            }
        }
    }
}