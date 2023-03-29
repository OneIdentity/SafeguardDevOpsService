using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OneIdentity.DevOps.Logic
{
    internal class RestoreManager
    {
        public static void CheckForStagedRestore()
        {
            if (Directory.Exists(WellKnownData.RestoreServiceStageDirPath))
            {
                // Clean out the current SafeguardDevOpsService directory
                foreach (var dir in Directory.GetDirectories(WellKnownData.ProgramDataPath))
                {
                    if (dir.Equals(WellKnownData.RestoreServiceStageDirPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete directory {dir} during restore process.");
                        Console.WriteLine(ex.ToString());
                    }
                }

                foreach (var file in Directory.GetFiles(WellKnownData.ProgramDataPath))
                {
                    try {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete file {file} during restore process.");
                        Console.WriteLine(ex.ToString());
                    }
                }

                // Restore the contents of the RestoreServiceStaging directory to SafeguardDevOpsService directory
                foreach (var dir in Directory.GetDirectories(WellKnownData.RestoreServiceStageDirPath))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    try {
                        Directory.Move(dir, Path.Combine(WellKnownData.ProgramDataPath, dirInfo.Name));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to move directory {dir} during restore process.");
                        Console.WriteLine(ex.ToString());
                    }
                }

                var moveList = new[]
                    { WellKnownData.ServiceIdentifier, WellKnownData.AppSettings + ".json" };
                var ignoreList = new[]
                    { WellKnownData.DBPasswordFileName };

                foreach (var file in Directory.GetFiles(WellKnownData.RestoreServiceStageDirPath))
                {
                    var f = new FileInfo(file);
                    if (moveList.Contains(f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        try {
                            File.Move(file, Path.Combine(WellKnownData.ServiceDirPath, f.Name), true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to move file {file} during restore process.");
                            Console.WriteLine(ex.ToString());
                        }
                        continue;
                    }
                    if (ignoreList.Contains(f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try {
                        File.Move(file, Path.Combine(WellKnownData.ProgramDataPath, f.Name));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to move file {file} during restore process.");
                        Console.WriteLine(ex.ToString());
                    }
                }

                // Delete the staged restore directory
                try {
                    Directory.Delete(WellKnownData.RestoreServiceStageDirPath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to delete the restore staging directory during restore process.");
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
