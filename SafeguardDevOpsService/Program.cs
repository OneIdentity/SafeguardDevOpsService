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
        private static readonly string ServiceIdentifier = "SafeguardDevOpsService";
        private static readonly string ServiceDescription =
            "Safeguard for Privileged Passwords DevOps integration service.";

        private static void Main()
        {
            Directory.CreateDirectory(WellKnownData.ProgramDataPath);
            var logDirPath = Path.Combine(WellKnownData.ProgramDataPath, $"{ServiceIdentifier}.log");

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
                hostConfig.SetDisplayName(ServiceIdentifier);
                hostConfig.SetServiceName(ServiceIdentifier);
                hostConfig.SetDescription(ServiceDescription);
                hostConfig.EnableServiceRecovery(recoveryOption =>
                {
                    recoveryOption.RestartService(0);
                });
            });
        }
    }
}
