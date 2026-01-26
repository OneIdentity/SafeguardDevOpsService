using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
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
            // Before doing anything, check if there is a staged restore.
            RestoreManager.CheckForStagedRestore();

            Directory.CreateDirectory(WellKnownData.ProgramDataPath);
            var logDirPath = WellKnownData.LogDirPath;

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(WellKnownData.AppSettingsFile, optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var rollingInterval = RollingInterval.Day;
            if (Enum.TryParse(configuration["LogRollingInterval"] ?? "Day", out RollingInterval interval))
            {
                rollingInterval = interval;
            }

            int? logFileCountLimit = 31;
            if (int.TryParse(configuration["LogFileCountLimit"], out int limit))
            {
                logFileCountLimit = limit == 0 ? null : limit;
            }

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logDirPath, shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u1}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: rollingInterval,
                    retainedFileCountLimit: logFileCountLimit)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .MinimumLevel.ControlledBy(LogLevelSwitcher.Instance.LogLevelSwitch)
                .CreateLogger();

            Console.WriteLine($"Safeguard Secrets Broker for DevOps logging to: {logDirPath}");
            if (rollingInterval != RollingInterval.Infinite)
            {
                Console.WriteLine($" - Logs will roll every {rollingInterval}.");
                if (logFileCountLimit.HasValue)
                {
                    Console.WriteLine($" - Only the {logFileCountLimit} most recent log files, including the current one, will be retained.");
                }
            }
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
