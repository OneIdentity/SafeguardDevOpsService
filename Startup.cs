using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneIdentity.SafeguardDevOpsService.ConfigDb;
using OneIdentity.SafeguardDevOpsService.ConfigurationImpl;
using Microsoft.OpenApi.Models;

namespace OneIdentity.SafeguardDevOpsService
{
    internal class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional:true, reloadOnChange:true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
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
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo {Title = "DevOps Service API", Version = "v1"}); });
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
            builder.RegisterType<ConfigurationLogic>().As<IConfigurationLogic>();
        }

        // Configure is where you add middleware. This is called after
        // ConfigureContainer. You can use IApplicationBuilder.ApplicationServices
        // here if you need to resolve things from the container.
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "DevOps Service API V1");
            });

            app.UseMvc();
        }
    }
}
