using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using VerticalTec.POS.Database;
using vtecdbhelper;

namespace VerticalTec_POS_Report_Dashboard
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var dbConfig = Configuration.GetSection("Database");
            var dbServer = dbConfig.GetValue<string>("DbServer");
            var dbName = dbConfig.GetValue<string>("DbName");
            var connString = $"Data Source={dbServer};Initial Catalog={dbName};User ID=vtecPOS; Password=vtecpwnet";
            services.AddSingleton<IDbHelper>(new vtecdbhelper.SqlServerDatabase(connString));
            services.AddSingleton<IDatabase>(new VerticalTec.POS.Database.SqlServerDatabase(dbServer, dbName));

            services.AddMvc().AddRazorPagesOptions(option => 
            {
                option.Conventions.AddPageRoute("/Landing", "/");
            }).AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Report API", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Report API V1");
            });
            app.UseStaticFiles();
            app.UseMvc();
        }
    }
}
