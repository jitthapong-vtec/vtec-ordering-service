using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IDatabase, MySqlDatabase>();
                services.AddSingleton<IClientConnectionService, ClientConnectionService>();
                services.AddSingleton<IDbstructureUpdateService, DbStructureUpdateService>();
                services.AddSingleton<IDownloadService, DownloadService>();
                services.AddSingleton<LiveUpdateDbContext>();
                services.AddSingleton<FrontConfigManager>();
                services.AddSingleton<VtecPOSEnv>();
                services.AddSingleton<BackupService>();
                services.AddHostedService<LiveUpdateService>();
                //services.AddHostedService<UpdateCheckerScheduleService>();
            });
    }
}
