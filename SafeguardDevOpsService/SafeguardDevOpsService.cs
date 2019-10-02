using System.Collections.Generic;
using System.IO;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using OneIdentity.SafeguardDevOpsService.Data;
using OneIdentity.SafeguardDevOpsService.Plugins;
using System.Linq;


namespace OneIdentity.SafeguardDevOpsService
{
    internal class SafeguardDevOpsService
    {
        private readonly IWebHost _host;
        private readonly IEnumerable<IService> _services;

        public SafeguardDevOpsService()
        {
            

            _host = new WebHostBuilder()
                .UseKestrel()
                .ConfigureServices(services => services.AddAutofac())
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            _services = (IEnumerable<IService>)_host.Services.GetService(typeof(IEnumerable<IService>));
        }

        public void Start()
        {
            _services.ToList().ForEach(s => s.Run());
            _host.Run();
        }

        public void Stop()
        {
            _host.StopAsync().Wait();
        }
    }
}
