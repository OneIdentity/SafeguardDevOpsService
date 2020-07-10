using System;
using System.IO;
using OneIdentity.DevOps.Logic;
using Serilog;
using Topshelf;

namespace OneIdentity.DevOps
{
    internal class Program
    {
        private static void Main()
        {
            Directory.CreateDirectory(WellKnownData.ProgramDataPath);
            var logDirPath = Path.Combine(WellKnownData.ProgramDataPath, "SafeguardDevOpsService.log");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logDirPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Console.WriteLine($"DevOps Service logging to: {logDirPath}");
            RestartManager.Instance.ShouldRestart = false;

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
                hostConfig.EnableServiceRecovery(recoveryOption =>
                {
                    recoveryOption.RestartService(0);
                });
            });
        }
    }
}
