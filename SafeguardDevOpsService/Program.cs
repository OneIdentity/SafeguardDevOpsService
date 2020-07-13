using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using OneIdentity.DevOps.Logic;
using Serilog;
using Topshelf;
using Topshelf.Runtime.DotNetCore;

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
                .Enrich.FromLogContext()
                .CreateLogger();

            Console.WriteLine($"DevOps Service logging to: {logDirPath}");
            RestartManager.Instance.ShouldRestart = false;

            HostFactory.Run(hostConfig =>
            {
                hostConfig.UseSerilog();
                hostConfig.Service<SafeguardDevOpsService>(service =>
                {
                    service.ConstructUsing(c => new SafeguardDevOpsService());
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    hostConfig.UseEnvironmentBuilder(c => new DotNetCoreEnvironmentBuilder(c));
                }
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
