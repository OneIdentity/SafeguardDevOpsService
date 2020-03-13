using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Logic;
using Microsoft.OpenApi.Models;

namespace OneIdentity.DevOps
{
    internal class Startup
    {
        private const string AppSettings = "appsettings";

        private static readonly string ServiceName = "Safeguard DevOps Service";
        private static readonly string ApiName = $"{ServiceName} API";
        private static readonly string ApiVersion = "v1";
        private static readonly string VersionApiName = $"{ApiName} {ApiVersion}";

        private static readonly string RoutePrefix = "service/devops";
        private static readonly string SwaggerRoutePrefix = $"{RoutePrefix}/swagger";
        private static readonly string SwaggerRouteTemplate = $"/{SwaggerRoutePrefix}/{{documentName}}/swagger.json";
        private static readonly string OpenApiRelativeUrl = $"/{SwaggerRoutePrefix}/{ApiVersion}/swagger.json";


        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile($"{AppSettings}.json", optional:true, reloadOnChange:true)
                .AddJsonFile($"{AppSettings}.{env.EnvironmentName}.json", optional: true)
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
                    Version = ApiVersion
                });
            });
            services.AddSwaggerGenNewtonsoftSupport();
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
            builder.Register(c => new LiteDbConfigurationRepository()).As<IConfigurationRepository>().SingleInstance();
            builder.Register(c => new PluginManager(c.Resolve<IConfigurationRepository>())).As<IPluginManager>().SingleInstance();
            builder.Register(c => new SafeguardLogic(c.Resolve<IConfigurationRepository>())).As<ISafeguardLogic>().SingleInstance();
            builder.Register(c => new PluginsLogic(c.Resolve<IConfigurationRepository>(), c.Resolve<IPluginManager>())).As<IPluginsLogic>().SingleInstance();
            //builder.RegisterType<ConfigurationLogic>().As<IConfigurationLogic>();
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger(c => { c.RouteTemplate = SwaggerRouteTemplate; });

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(OpenApiRelativeUrl, VersionApiName);
                c.RoutePrefix = SwaggerRoutePrefix;
            });

            app.UseMvc();
        }
    }
}
