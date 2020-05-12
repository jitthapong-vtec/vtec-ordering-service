using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CurrieTechnologies.Razor.SweetAlert2;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;
using VerticalTec.POS.LiveUpdateConsole.Hubs;

namespace VerticalTec.POS.LiveUpdateConsole
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

            services.AddRazorPages();
            services.AddSignalR();
            services.AddServerSideBlazor();
            services.AddSweetAlert2();
            services.AddSingleton<IDatabase>(db => new SqlServerDatabase(connStr));
            services.AddSingleton<LiveUpdateDbContext>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapHub<ConsoleHub>("/console");
                endpoints.MapHub<LiveUpdateHub>("/liveupdate");
            });
        }
    }
}
