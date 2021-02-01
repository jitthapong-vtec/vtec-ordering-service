using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;
using VerticalTec.POS.LiveUpdateConsole.Models;
using VerticalTec.POS.LiveUpdateConsole.Services;

namespace VerticalTec.POS.LiveUpdateConsole.Pages
{
    public class FormVersionDeployModel : PageModel
    {
        private readonly IWebHostEnvironment _hostingEnvironment;

        private readonly IDatabase _db;
        private LiveUpdateDbContext _liveUpdateCtx;
        private RepoService _repoService;

        [BindProperty]
        public VersionDeploy VersionDeploy { get; set; }

        public FormVersionDeployModel(IDatabase db, LiveUpdateDbContext ctx, RepoService repoService, IWebHostEnvironment hostEnvironment)
        {
            _db = db;
            _liveUpdateCtx = ctx;
            _repoService = repoService;
            _hostingEnvironment = hostEnvironment;

            VersionDeploy = new VersionDeploy();
        }

        public async Task<IActionResult> OnGetAsync(string batchId)
        {
            using (var conn = await _db.ConnectAsync())
            {
                if (!string.IsNullOrEmpty(batchId))
                {
                    var versionDeploys = await _liveUpdateCtx.GetVersionDeploy(conn, batchId: batchId);
                    VersionDeploy = versionDeploys.FirstOrDefault();
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            using (var conn = await _db.ConnectAsync())
            {

            }
            return Page();
        }

        public ActionResult OnPostUploadPatch()
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
                        VersionDeploy.FileUrl = metaDataObject.FileName;

                        RemoveTempFilesAfterDelay(path);

                        return Page();
                    }
                }
                else
                {
                    return BadRequest("No metadata found");
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            return new OkResult();
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
