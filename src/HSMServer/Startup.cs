﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HSMServer.Authentication;
using HSMServer.ClientUpdateService;
using HSMServer.Configuration;
using HSMServer.DataLayer;
using HSMServer.Middleware;
using HSMServer.MonitoringServerCore;
using HSMServer.Products;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.PlatformAbstractions;

namespace HSMServer
{
    public class Startup
    {
        private IServiceCollection services;
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "CertificateValidationScheme";

                options.DefaultForbidScheme = "CertificateValidationScheme";

                options.AddScheme<CertificateSchemeHandler>("CertificateValidationScheme", "CertificateValidationScheme");
            });

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });


            services.AddGrpc();
            services.AddControllers();

            services.AddCors();

            //services.AddSingleton<IDatabaseClass, DatabaseClass>();
            services.AddSingleton<IDatabaseClass, LevelDBDatabaseClass>();
            services.AddSingleton<IProductManager, ProductManager>();
            services.AddSingleton<CertificateManager>();
            services.AddSingleton<UserManager>();
            services.AddSingleton<IBarSensorsStorage, BarSensorsStorage>();
            services.AddSingleton<IMonitoringCore, MonitoringCore>();
            services.AddSingleton<ClientCertificateValidator>();
            services.AddSingleton<IUpdateService, UpdateServiceCore>();
            services.AddSingleton<Services.HSMService>();
            services.AddSingleton<Services.AdminService>();
            //services.AddSingleton<SensorsController>();
            //services.AddSingleton<ValuesController>();

            services.AddHttpsRedirection(configureOptions =>
            {
                configureOptions.HttpsPort = 44330;
            });

            services.AddSwaggerGen();

            services.ConfigureSwaggerGen(options =>
            {
                var basePath = PlatformServices.Default.Application.ApplicationBasePath;
                var xmlPath = Path.Combine(basePath, "HSMServer.xml");
                options.IncludeXmlComments(xmlPath, true);
            });

            this.services = services;
        }       
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            var lifeTimeService = (IHostApplicationLifetime)app.ApplicationServices.GetService(typeof(IHostApplicationLifetime));
            lifeTimeService.ApplicationStopping.Register(OnShutdown, app.ApplicationServices);

            app.UseCertificateValidator();

            app.UseSwagger(c =>
            {
                //c.RouteTemplate = "api/swagger/swagger/{documentName}/swagger.json";
                c.SerializeAsV2 = true;
            });

            app.UseSwaggerUI(c =>
            {
                c.RoutePrefix = "api/swagger";
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "HSM server api");
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<Services.HSMService>();
                endpoints.MapGrpcService<Services.AdminService>();
              
                endpoints.MapGet("/Protos/sensors_service.proto", async context =>
                {
                    await context.Response.WriteAsync(
                        await System.IO.File.ReadAllTextAsync("Protos/sensors_service.proto"));
                });

                endpoints.MapControllers();
            });

            

            app.UseHttpsRedirection();

            //app.UseCors(x => x.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        }

        public void OnShutdown(object state)
        {
            var serviceProvider = (IServiceProvider) state;
            var objectToDispose = services
                .Where(s => s.Lifetime == ServiceLifetime.Singleton
                            && s.ImplementationInstance != null
                            && s.ServiceType.GetInterfaces().Contains(typeof(IMonitoringCore)))
                .Select(s => s.ImplementationInstance as IMonitoringCore).First();

            objectToDispose.Dispose();
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
    }
}
