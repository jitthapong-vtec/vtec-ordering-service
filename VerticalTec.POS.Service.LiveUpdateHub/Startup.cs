using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerticalTec.POS.Database;
using VerticalTec.POS.Share.LiveUpdate;

namespace VerticalTec.POS.Service.LiveUpdateHub
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var connStr = Configuration.GetConnectionString("VtecPOS");
            services.AddScoped<IDatabase>(db => new SqlServerDatabase(connStr));
            services.AddScoped<LiveUpdateDbContext>();
            services.AddSignalR();
            services.AddHostedService<LiveUpdateWorker>();
        }

        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<LiveUpdateHub>("/liveupdate");
            });
        }
    }
}
