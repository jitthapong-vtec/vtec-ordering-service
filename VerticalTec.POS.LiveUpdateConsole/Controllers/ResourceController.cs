using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

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
        public ActionResult UploadProductImage()
        {
            var file = Request.Form.Files["file"];
            var chunkMetadata = Request.Form["chunkMetadata"];

            var patchPath = @"Patch";
            try
            {
                var path = Path.Combine(_hostingEnvironment.WebRootPath, patchPath);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                if (!string.IsNullOrEmpty(chunkMetadata))
                {
                    var metaDataObject = JsonConvert.DeserializeObject<ChunkMetadata>(chunkMetadata);
                    var tempFilePath = Path.Combine(path, metaDataObject.FileGuid + ".tmp");

                    AppendChunkToFile(tempFilePath, file);
                    if (metaDataObject.Index == (metaDataObject.TotalCount - 1))
                    {
                        SaveUploadedFile(tempFilePath, Path.Combine(path, metaDataObject.FileName));

                        RemoveTempFilesAfterDelay(path);
                    }
                }
                else
                {
                    return BadRequest("No metadata found");
                }
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        void AppendChunkToFile(string path, IFormFile content)
        {
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write))
            {
                content.CopyTo(stream);
            }
        }

        void SaveUploadedFile(string tempFilePath, string destinationPath)
        {
            System.IO.File.Copy(tempFilePath, destinationPath, true);
        }

        void RemoveTempFilesAfterDelay(string path)
        {
            var dir = new DirectoryInfo(path);
            if (dir.Exists)
                foreach (var file in dir.GetFiles("*.tmp"))
                    file.Delete();
        }
    }

    public class ChunkMetadata
    {
        public int Index { get; set; }
        public int TotalCount { get; set; }
        public int FileSize { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FileGuid { get; set; }
    }
}
