using System;
using Audit.Core;
using Audit.EntityFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            var provider = services.BuildServiceProvider();

            DbContext DbContextBuilder(AuditEventEntityFramework ev)
            {
                var db = ev.EntityFrameworkEvent.GetDbContext().Database;
                var conn = db.GetDbConnection();
                var tran = db.CurrentTransaction;
                var auditContext = new MyDbContext(new DbContextOptionsBuilder<MyDbContext>()
                    .UseNpgsql(conn).Options);

                if (tran != null)
                {
                    auditContext.Database.UseTransaction(tran.GetDbTransaction());
                }

                return auditContext;
            }

            Audit.Core.Configuration.Setup()
                .UseEntityFramework(config => config
                    .UseDbContext(DbContextBuilder)
                    .AuditTypeMapper(x => x == typeof(AuditLogEntry) ? null : typeof(AuditLogEntry))
                    .AuditEntityAction<AuditLogEntry>((ev, entry, auditLog) => { })
                    .IgnoreMatchedProperties(true)
                )
                .WithCreationPolicy(EventCreationPolicy.Manual);
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