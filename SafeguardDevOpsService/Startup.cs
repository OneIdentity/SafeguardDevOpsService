using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autofac;
using AutofacSerilogIntegration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Logic;
using Microsoft.OpenApi.Models;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace OneIdentity.DevOps
{
    internal class Startup
    {

        private static readonly string ServiceName = "Safeguard Secrets Broker for DevOps";
        private static readonly string ApiName = $"{ServiceName} API";
        private static readonly string ApiVersion = "v1";
        private static readonly string VersionApiName = $"{ApiName} {ApiVersion}";
        private static readonly string ApiDescription = "Web API for controlling the distribution of secrets from Safeguard for Privileged Passwords " + 
                                                        "to third-party vaults and orchestration frameworks.  This gives your developers frictionless integration " +
                                                        "from their favorite DevOps tooling.";

        private static readonly string RoutePrefix = "service/devops";
        private static readonly string SwaggerRoutePrefix = $"{RoutePrefix}/swagger";
        private static readonly string SwaggerRouteTemplate = $"/{SwaggerRoutePrefix}/{{documentName}}/swagger.json";
        private static readonly string OpenApiRelativeUrl = $"/{SwaggerRoutePrefix}/{ApiVersion}/swagger.json";


        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile($"{WellKnownData.AppSettings}.json", optional:true, reloadOnChange:true)
                .AddJsonFile($"{WellKnownData.AppSettings}.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .AddNewtonsoftJson(opts => opts.UseMemberCasing())
                .AddMvcOptions(opts => { opts.EnableEndpointRouting = false; });

            services.AddSwaggerGen(c => {
                c.SwaggerDoc(ApiVersion, new OpenApiInfo
                {
                    Title = ApiName,
                    Version = ApiVersion,
                    Description = ApiDescription
                });
                c.EnableAnnotations();
                c.AddSecurityDefinition("spp-token", new OpenApiSecurityScheme()
                {
                    Description = "Authorization header using the spp-token scheme",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "spp-token"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "spp-token"
                            },
                            Scheme = "spp-token",
                            Name = "spp-token",
                            In = ParameterLocation.Header,
                        },
                        new List<string>()
                    }
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            services.AddSwaggerGenNewtonsoftSupport();

            // In production, the Angular files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = Path.Combine(AppContext.BaseDirectory, "ClientApp/dist");
            });
        }

        // This only gets called if your environment is Development. The
        // default ConfigureServices won't be automatically called if this
        // one is called.
        public void ConfigureDevelopmentServices(IServiceCollection services)
        {
            // Add things to the service collection that are only for the
            // development environment.
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterLogger();
            builder.Register(c => new LiteDbConfigurationRepository()).As<IConfigurationRepository>().SingleInstance();
            builder.Register(c => new SafeguardLogic(c.Resolve<IConfigurationRepository>())).As<ISafeguardLogic>().SingleInstance();
            builder.Register(c => new PluginManager(c.Resolve<IConfigurationRepository>(), c.Resolve<ISafeguardLogic>())).As<IPluginManager>().SingleInstance();
            builder.Register(c => new PluginsLogic(c.Resolve<IConfigurationRepository>(), c.Resolve<IPluginManager>(), c.Resolve<ISafeguardLogic>())).As<IPluginsLogic>().SingleInstance();
            builder.Register(c => new MonitoringLogic(c.Resolve<IConfigurationRepository>(), c.Resolve<IPluginManager>())).As<IMonitoringLogic>().SingleInstance();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            // Enable middleware for request logging
            app.UseSerilogRequestLogging();

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger(c => { c.RouteTemplate = SwaggerRouteTemplate; });

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(OpenApiRelativeUrl, VersionApiName);
                c.RoutePrefix = SwaggerRoutePrefix;
                c.HeadContent = "https://github.com/OneIdentity/SafeguardDevOpsService";
                c.DisplayRequestDuration();
                c.DefaultModelRendering(ModelRendering.Example);
                c.ShowExtensions();
                c.ShowCommonExtensions();
            });

            app.UseExceptionHandler("/Error");
            app.UseHttpsRedirection();
            app.UseMvc();

            app.UseStaticFiles();
            app.UseSpaStaticFiles();
            app.UseSpa(spa =>
            {
                // To learn more about options for serving an Angular SPA from ASP.NET Core,
                // see https://go.microsoft.com/fwlink/?linkid=864501

                spa.Options.SourcePath = "ClientApp";
            });
        }
    }
}
