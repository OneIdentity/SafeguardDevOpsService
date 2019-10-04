using Serilog;
using Topshelf;

namespace OneIdentity.SafeguardDevOpsService
{
    class Program
    {
        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("devopslog.txt", outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            HostFactory.Run(hostConfig =>
            {
                hostConfig.Service<SafeguardDevOpsService>(service =>
                {
                    service.ConstructUsing(c => new SafeguardDevOpsService());
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });
                hostConfig.UseSerilog();
                hostConfig.StartAutomaticallyDelayed();
                hostConfig.SetDisplayName("SafeguardDevOpsService");
                hostConfig.SetServiceName("SafeguardDevOpsService");
                hostConfig.SetDescription("Safeguard for Privileged Passwords DevOps integration service.");
            });
        }
    }
}
