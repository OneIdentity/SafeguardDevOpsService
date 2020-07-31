using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ConstructVersionNumber
{
    class Program
    {
        static void Main(string[] args)
        {
            var major = 1;
            var minor = 0;
            var revision = 0;
            var build = 0;

            if (args.Length < 1)
            {
                Environment.Exit(1);
            }

            var buildId = Environment.GetEnvironmentVariable("BuildId");

            var startingDir = args[0];
            var allFiles = Directory.GetFiles(startingDir, "AssemblyInfo.tmpl", SearchOption.AllDirectories);

            var pattern = new Regex("AssemblyVersion\\(\"(?<major>\\d+)\\.(?<minor>\\d+)\\.(?<revision>\\d+)\\.(?<build>\\d+)\"\\)");

            foreach (var templateFile in allFiles)
            {
                var path = Path.GetDirectoryName(templateFile);
                var assemblyInfoFile = Path.Combine(path, "AssemblyInfo.cs");

                if (File.Exists(assemblyInfoFile))
                {
                    var assemblyInfo = File.ReadAllText(assemblyInfoFile);
                    var matches = pattern.Matches(assemblyInfo);

                    if (matches.Count == 1)
                    {
                        major = Convert.ToInt32(matches[0].Groups["major"].Value);
                        minor = Convert.ToInt32(matches[0].Groups["minor"].Value);
                        revision = Convert.ToInt32(matches[0].Groups["revision"].Value);
                        build = Convert.ToInt32(matches[0].Groups["build"].Value);
                    }
                }

                build = buildId == null ? build + 1 : Convert.ToInt32(buildId);

                var newVersion = $"{major}.{minor}.{revision}.{build}";
                var template = File.ReadAllText(templateFile);
                template = template.Replace("<version>", newVersion);

                File.Delete(assemblyInfoFile);
                File.WriteAllText(assemblyInfoFile, template);
            }
        }
    }
}
