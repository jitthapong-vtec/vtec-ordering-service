using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using System.Threading.Tasks;
using Blazored.SessionStorage;
using CurrieTechnologies.Razor.SweetAlert2;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerticalTec.POS.Database;
using VerticalTec.POS.LiveUpdate;
using VerticalTec.POS.LiveUpdateConsole.Hubs;
using VerticalTec.POS.LiveUpdateConsole.Models;
using VerticalTec.POS.LiveUpdateConsole.Services;

namespace VerticalTec.POS.LiveUpdateConsole
{
    public class Startup
    {
        static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var connStr = Configuration.GetConnectionString("VtecPOS");

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            services.AddDevExpressBlazor();
            services.AddRazorPages();

            var redisConStr = Configuration["Cached:Redis"];
            if (string.IsNullOrEmpty(redisConStr) == false)
            {
                services.AddSignalR(opts =>
                {
                    opts.EnableDetailedErrors = true;
                }).AddStackExchangeRedis(redisConStr);
            }
            else
            {
                services.AddSignalR(opts =>
                {
                    opts.EnableDetailedErrors = true;
                });
            }
            
            services.AddServerSideBlazor();
            services.AddSweetAlert2();
            services.AddBlazoredSessionStorage();

            services.AddSingleton<IDatabase>(db => new SqlServerDatabase(connStr));
            services.AddSingleton<LiveUpdateDbContext>();
            services.AddSingleton<RepoService>();
            services.AddScoped<AuthenticationStateProvider, AuthenStateProvider>();
            services.AddSingleton<IClientConnectionService, ClientConnectionService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseExceptionHandler(configure =>
            {
                configure.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    var handler = context.Features.Get<IExceptionHandlerFeature>();
                    if (handler != null)
                    {
                        var ex = handler.Error;
                        if (handler.Error.InnerException != null)
                            ex = handler.Error.InnerException;

                        _logger.Error(ex);
                    }
                    return Task.CompletedTask;
                });
            });

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapHub<ConsoleHub>("/console");
                endpoints.MapHub<LiveUpdateHub>("/liveupdate");
            });
        }
    }
}
