using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.Data.Sqlite;

namespace JsonAuditor
{
    public class Startup
    {
        private readonly object sqliteLock = new object();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "JsonAuditor", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // if (env.IsDevelopment())
            // {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "JsonAuditor v1"));
            // }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            ConfigureAndCreateDb();
        }

        private void ConfigureAndCreateDb()
        {
            lock (sqliteLock)
            {
                bool tableExists = false;

                using (var connection = new SqliteConnection("Data Source=Auditor.db"))
                {
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(name) FROM sqlite_master WHERE type='table' AND name='auditRecords'";

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var present = reader.GetString(0);
                            if (present != "0")
                            {
                                tableExists = true;
                            }
                        }
                    }

                    if (!tableExists)
                    {
                        string tableSql = @"CREATE TABLE 
                    'auditRecords' (
                        'AuditId' STRING,
                        'ParentAuditId' STRING,
                        'EntityId' STRING NOT NULL,
                        'EntityType' INTEGER NOT NULL,
                        'TransactionTime' INTEGER,
                        'AuditTime' INTEGER NOT NULL,
                        'AutoResolved' INTEGER NULL,
                        'Record' TEXT NOT NULL,
                        PRIMARY KEY('AuditId')
                    )
                    ";

                        command = connection.CreateCommand();
                        command.CommandText = tableSql;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
