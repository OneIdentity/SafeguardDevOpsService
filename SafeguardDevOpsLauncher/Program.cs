using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace OneIdentity.DevOps
{
    class Program
    {
        private const string DevOpsServiceName = "SafeguardDevOpsService";
        private const string DevOpsServiceNameExe = DevOpsServiceName + ".exe";
        private static readonly string WorkingDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        private static readonly string DevOpsServiceLauncherPath = Path.Combine(WorkingDirectory, DevOpsServiceNameExe);
        private static readonly string DevOpsServicePath = Path.Combine(WorkingDirectory, "..", DevOpsServiceName, DevOpsServiceNameExe);
        private static readonly string RestartDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DevOpsServiceName, "ShouldRestart.log");

        static void Main(string[] args)
        {
            Console.WriteLine($"Working directory: {WorkingDirectory}");

            string servicePath = null;
            if (File.Exists(DevOpsServiceLauncherPath))
                servicePath = DevOpsServiceLauncherPath;
            else if (File.Exists(DevOpsServicePath))
                servicePath = DevOpsServicePath;
            else
                Console.WriteLine($"Failed to find {DevOpsServiceNameExe}");

            var processInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = servicePath
            };

            var shouldRestart = false;
            do
            {
                shouldRestart = false;
                File.Delete(RestartDataPath);

                Console.WriteLine("Starting child process...");
                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();

                    if (File.Exists(RestartDataPath))
                        shouldRestart = true;
                }
            } while (shouldRestart);
        }

    }
}
