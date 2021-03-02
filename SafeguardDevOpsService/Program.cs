using System;
using System.IO;
using System.Runtime.InteropServices;
using OneIdentity.DevOps.Logic;
using Serilog;
using Topshelf;
using Topshelf.Runtime.DotNetCore;

namespace OneIdentity.DevOps
{
    internal class Program
    {
        private static readonly string ServiceDescription =
            "Safeguard for Privileged Passwords DevOps integration service.";

        private static void Main()
        {
            Directory.CreateDirectory(WellKnownData.ProgramDataPath);
            var logDirPath = WellKnownData.LogDirPath;

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logDirPath, shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .MinimumLevel.ControlledBy(LogLevelSwitcher.Instance.LogLevelSwitch)
                .CreateLogger();

            Console.WriteLine($"Safeguard Secrets Broker for DevOps logging to: {logDirPath}");
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
                hostConfig.SetDisplayName(WellKnownData.DevOpsServiceName);
                hostConfig.SetServiceName(WellKnownData.DevOpsServiceName);
                hostConfig.SetDescription(ServiceDescription);
                hostConfig.EnableServiceRecovery(recoveryOption =>
                {
                    recoveryOption.RestartService(0);
                });
            });
        }
    }
}
