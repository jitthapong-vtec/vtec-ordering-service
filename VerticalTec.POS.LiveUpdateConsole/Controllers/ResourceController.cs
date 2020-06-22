using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace VerticalTec.POS.LiveUpdateConsole.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResourceController : ControllerBase
    {
        private readonly IWebHostEnvironment _hostingEnvironment;

        public ResourceController(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpPost("UploadPatch")]
        public async Task<ActionResult<object>> UploadProductImage(IFormFile file)
        {
            var patchPath = @"Patch";
            try
            {
                var path = Path.Combine(_hostingEnvironment.WebRootPath, patchPath);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                var filePath = Path.Combine(path, file.FileName);
                using (var fileStream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(fileStream);
                }
            }
            catch
            {
                return BadRequest();
            }
            return Ok();
        }
    }
}
