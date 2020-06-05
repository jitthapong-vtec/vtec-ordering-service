using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class BackupService
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        VtecPOSEnv _posEnv;

        public BackupService(VtecPOSEnv posEnv)
        {
            _posEnv = posEnv;
        }

        public void Backup(string patchFileName, string backupFileName)
        {
            List<string> filesInPatch = new List<string>();
            using(var archive = ZipFile.OpenRead(patchFileName))
            {
                foreach(var entry in archive.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        filesInPatch.Add(entry.FullName);
                        _logger.LogInfo($"Files in patch {entry.FullName}");
                    }
                }
            }

            if (!filesInPatch.Any())
                throw new ArgumentException($"No file in {patchFileName}");

            var programFiles = Directory.GetFiles(_posEnv.FrontCashierPath, "*.*", SearchOption.AllDirectories);
            for (var i = 0; i < programFiles.Count(); i++)
            {
                programFiles[i] = programFiles[i].Replace(_posEnv.FrontCashierPath, "");
                programFiles[i] = programFiles[i].Replace("\\", "/");
            }
            var selectFiles = programFiles.Where(programFile => filesInPatch.Contains(programFile));
            if (selectFiles?.Any() == false)
                throw new ArgumentException("Patch file not match!");

            using(var stream = new FileStream(backupFileName, FileMode.Create))
            {
                using(var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (var file in selectFiles)
                    {
                        archive.CreateEntryFromFile(Path.Combine(_posEnv.FrontCashierPath, file), file);
                    }
                }
            }
        }
    }
}
