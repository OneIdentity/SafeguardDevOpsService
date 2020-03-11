using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Logic;
using Microsoft.OpenApi.Models;
using OneIdentity.DevOps.Attributes;
using OneIdentity.DevOps.Plugins;

namespace OneIdentity.DevOps
{
    internal class Startup
    {
        private const string AppSettings = "appsettings";

        private static readonly string ServiceName = "Safeguard DevOps Service";
        private static readonly string ApiName = $"{ServiceName} API";
        private static readonly string ApiVersion = "v1";
        private static readonly string VersionApiName = $"{ApiName} {ApiVersion}";

        private static readonly string SwaggerRoutePrefix = $"{WellKnownData.RoutePrefix}/swagger";
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

        // ConfigureServices is where you register dependencies. This gets
        // called by the runtime before the ConfigureContainer method, below.
        public void ConfigureServices(IServiceCollection services)
        {

            // Add services to the collection. Don't build or return
            // any IServiceProvider or the ConfigureContainer method
            // won't get called.
            services.AddMvc()
                .AddMvcOptions(opts => { opts.EnableEndpointRouting = false; });
                //.SetCompatibilityVersion(CompatibilityVersion.Version_2_2);


            services.AddSwaggerGen(c => { 
                c.SwaggerDoc(ApiVersion, new OpenApiInfo
                {
                    Title = ApiName,
                    Version = ApiVersion
                });
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

        // ConfigureContainer is where you can register things directly
        // with Autofac. This runs after ConfigureServices so the things
        // here will override registrations made in ConfigureServices.
        // Don't build the container; that gets done for you. If you
        // need a reference to the container, you need to use the
        // "Without ConfigureContainer" mechanism shown later.
        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.Register(c => new LiteDbConfigurationRepository()).As<IConfigurationRepository>().SingleInstance();
            builder.Register(c => new PluginManager(c.Resolve<IConfigurationRepository>())).As<IPluginManager>().SingleInstance();

            builder.RegisterType<ConfigurationLogic>().As<IConfigurationLogic>();
        }

        // Configure is where you add middleware. This is called after
        // ConfigureContainer. You can use IApplicationBuilder.ApplicationServices
        // here if you need to resolve things from the container.
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
