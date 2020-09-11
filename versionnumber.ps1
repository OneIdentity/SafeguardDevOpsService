[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$StartingDir,
    [Parameter(Mandatory=$true, Position=1)]
    [string]$BuildId
)

if (-not ([System.Management.Automation.PSTypeName]"Ex.VersionNumberEditor").Type)
{
    Add-Type -WarningAction SilentlyContinue -TypeDefinition @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Ex
{
public class VersionNumberEditor {
    public string SetVersionNumber(string buildId, string startingDir) {
        var major = 1; var minor = 0; var revision = 0; var build = 0;

        Console.WriteLine("BuildId=[{0}] startingDir=[{1}]", buildId, startingDir);
        if (buildId.StartsWith("-"))
            buildId = null;

        var allFiles = Directory.GetFiles(startingDir, "AssemblyInfo.tmpl", SearchOption.AllDirectories);
        var pattern = new Regex("AssemblyVersion\\(\"(?<major>\\d+)\\.(?<minor>\\d+)\\.(?<revision>\\d+)\\.(?<build>\\d+)\"\\)");

        foreach (var templateFile in allFiles) {
            Console.WriteLine("TemplateFile={0}", templateFile);
            var path = Path.GetDirectoryName(templateFile);
            var assemblyInfoFile = Path.Combine(path, "AssemblyInfo.cs");
            Console.WriteLine("AssemblyInfoFile={0}", assemblyInfoFile);

            if (File.Exists(assemblyInfoFile)) {
                var assemblyInfo = File.ReadAllText(assemblyInfoFile);
                var matches = pattern.Matches(assemblyInfo);

                if (matches.Count == 1) {
                    major = Convert.ToInt32(matches[0].Groups["major"].Value);
                    minor = Convert.ToInt32(matches[0].Groups["minor"].Value);
                    revision = Convert.ToInt32(matches[0].Groups["revision"].Value);
                    build = Convert.ToInt32(matches[0].Groups["build"].Value);
                }
            }

            build = string.IsNullOrEmpty(buildId) ? build + 1 : Convert.ToInt32(buildId) % UInt16.MaxValue;

            var newVersion = string.Format("{0}.{1}.{2}.{3}", major, minor, revision, build);
            var template = File.ReadAllText(templateFile);
            template = template.Replace("<version>", newVersion);

            File.Delete(assemblyInfoFile);
            File.WriteAllText(assemblyInfoFile, template);
            Console.WriteLine("*****");
            Console.Write(template);
            Console.WriteLine("*****");
            return newVersion;
        }
    }
}
}
"@
}

$local:VersEditor = (New-Object Ex.VersionNumberEditor)
$local:VersString = $local:VersEditor.SetVersionNumber($buildId, $StartingDir)
Write-Output "##vso[task.setvariable variable=VersionString;]$($local:VersString)"
