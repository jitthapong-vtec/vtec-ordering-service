using Microsoft.Extensions.Hosting;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class DbStructureUpdateService : IDbstructureUpdateService
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        IDatabase _db;
        LiveUpdateDbContext _liveUpdateContext;
        FrontConfigManager _frontConfigManager;

        public DbStructureUpdateService(IDatabase db, LiveUpdateDbContext context, FrontConfigManager configManager)
        {
            _db = db;
            _liveUpdateContext = context;
            _frontConfigManager = configManager;
        }

        public async Task UpdateStructureAsync(string downloadPath)
        {
            try
            {
                var sqlPath = "";
                using (var conn = await _db.ConnectAsync())
                {
                    var posSetting = _frontConfigManager.POSDataSetting;
                    var versionDeploy = await _liveUpdateContext.GetActiveVersionDeploy(conn);
                    
                    var extractPath = Path.Combine(Path.GetTempPath(), $"Scripts");

                    if (!Directory.Exists(extractPath))
                        Directory.CreateDirectory(extractPath);

                    using (var archive = ZipFile.Open(downloadPath, ZipArchiveMode.Update))
                    {
                        var entry = archive.Entries.Where(a => a.FullName.EndsWith("scripts.sql", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
                        if (entry != null)
                        {
                            _logger.LogInfo($"found sql file {entry.Name}");
                            sqlPath = Path.GetFullPath(Path.Combine(extractPath, entry.Name));
                            entry.ExtractToFile(sqlPath, true);
                            entry.Delete();
                            _logger.LogInfo($"extract success ready for execute");
                        }
                    }

                    if (string.IsNullOrEmpty(sqlPath))
                        return;

                    var content = File.ReadAllText(sqlPath);
                    var cmd = new MySqlCommand("", conn as MySqlConnection);
                    foreach (var line in content.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.IsNullOrEmpty(line))
                            continue;
                        try
                        {
                            _logger.LogInfo($"exec {line}");
                            cmd.CommandText = line;
                            cmd.ExecuteNonQuery();
                            _logger.LogInfo($"exec success");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error exec sql {ex.Message}", ex);
                        }
                    }
                }
            }catch(Exception ex)
            {
                _logger.LogError("Error UpdateStructureAsync", ex);
            }
        }
    }
}
