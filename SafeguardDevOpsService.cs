using System.IO;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace OneIdentity.SafeguardDevOpsService
{
    internal class SafeguardDevOpsService
    {
        private readonly IWebHost _host;

        public SafeguardDevOpsService()
        {
            _host = new WebHostBuilder()
                .UseKestrel()
                .ConfigureServices(services => services.AddAutofac())
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();
        }

        public void Start()
        {
            _host.Run();
        }

        public void Stop()
        {
            _host.StartAsync().Wait();
        }
    }
}
