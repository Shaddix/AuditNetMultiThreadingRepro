using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Audit.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace AuditNetMultiThreadingRepro
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
            services
                .AddEntityFrameworkNpgsql()
                .AddDbContext<MyDbContext>(
                    (provider, opt) => opt
                        .UseNpgsql(Configuration.GetConnectionString("DefaultConnection"))
                    ,
                    contextLifetime: ServiceLifetime.Scoped,
                    optionsLifetime: ServiceLifetime.Singleton);
            services
                .AddSingleton<Func<MyDbContext>>(
                    provider => () => new MyDbContext(
                        provider.GetRequiredService<DbContextOptions<MyDbContext>>()
                    ));
            ConfigureAudit(services);
            
            
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1",
                    new OpenApiInfo { Title = "AuditNetMultiThreadingRepro", Version = "v1" });
            });
        }


        private void ConfigureAudit(IServiceCollection services)
        {
            Audit.Core.Configuration.Setup()
                .UseEntityFramework(config => config
                    .AuditTypeMapper(x => x == typeof(AuditLogEntry) ? null : typeof(AuditLogEntry))
                    .AuditEntityAction<AuditLogEntry>((ev, entry, auditLog) => { })
                    .IgnoreMatchedProperties(true));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                    c.SwaggerEndpoint("/swagger/v1/swagger.json",
                        "AuditNetMultiThreadingRepro v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            context.Database.EnsureCreated();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}