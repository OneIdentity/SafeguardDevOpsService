using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    internal class AddonLogic : IAddonLogic
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private IDeployAddon _deployAddon;
        private IUndeployAddon _undeployAddon;


        public AddonLogic(IConfigurationRepository configDb)
        {
            _logger = Serilog.Log.Logger;
            _configDb = configDb;
        }

        private DevOpsException LogAndException(string msg, Exception ex = null)
        {
            _logger.Error(msg);
            return new DevOpsException(msg, ex);
        }


        public void InstallAddon(IFormFile formFile)
        {
            if (formFile.Length <= 0)
                throw LogAndException("Add-on cannot be null or empty");

            try
            {
                using (var inputStream = formFile.OpenReadStream())
                using (var zipArchive = new ZipArchive(inputStream, ZipArchiveMode.Read))
                {
                    InstallAddon(zipArchive);
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to install the Add-on plugin. {ex.Message}");
            }
        }

        public void RemoveAddon()
        {
            _logger.Information("Saving the remove token to disk. The Add-on will be delete after a reboot.");
            try
            {
                using (File.Create(WellKnownData.RemoveAddonFilePath)) {}
            }
            catch {}

            UndeployAddon();
        }

        // public void CleanUpDeletedPlugins()
        // {
        //     // If the DeleteAddon.all file exists, just remove the entire addon directory.
        //     if (File.Exists(WellKnownData.RemoveAddonFilePath))
        //     {
        //         try
        //         {
        //             Directory.Delete(WellKnownData.AddonServiceDirPath, true);
        //         }
        //         catch (Exception ex)
        //         {
        //             _logger.Error($"Failed to clean up the {WellKnownData.AddonServiceDirPath} directory. {ex.Message}");
        //         }
        //     }
        // }

        private void InstallAddon(ZipArchive zipArchive)
        {
            var manifestEntry = zipArchive.GetEntry(WellKnownData.ManifestPattern);
            if (manifestEntry == null)
            {
                throw LogAndException("Failed to find the manifest for the add-on.");
            }

            using (var reader = new StreamReader(manifestEntry.Open()))
            {
                var manifest = reader.ReadToEnd();
                var pluginManifest = JsonHelper.DeserializeObject<PluginManifest>(manifest);
                if (pluginManifest != null)
                {
                    RestartManager.Instance.ShouldRestart = true;
                    zipArchive.ExtractToDirectory(WellKnownData.AddonServiceStageDirPath, true);

                    DeployAddon(pluginManifest.Assembly, WellKnownData.AddonServiceStageDirPath);
                }
                else
                {
                    throw LogAndException(
                        $"Add-on package does not contain a {WellKnownData.ManifestPattern} file.");
                }
            }
        }

        private void DeployAddon(string assemblyName, string tempFolder)
        {
            try
            {
                var addonPath = Path.Combine(tempFolder, assemblyName);
                if (!File.Exists(addonPath))
                {
                    throw LogAndException("Failed to find the add-on module.");
                }

                var assembly = Assembly.LoadFrom(addonPath);

                var deployAddonClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass 
                                                                   && t.Name.Equals(WellKnownData.DeployAddonClassName) 
                                                                   && typeof(IDeployAddon).IsAssignableFrom(t));

                if (deployAddonClass != null)
                {
                    _logger.Information($"Loading the Add-on service from path {addonPath}.");
                    var deployAddon = (IDeployAddon) Activator.CreateInstance(deployAddonClass);

                    if (deployAddon == null)
                    {
                        _logger.Warning($"Unable to instantiate the Add-on service from {addonPath}");
                    }
                    else
                    {
                        _deployAddon = deployAddon;
                        _deployAddon.SetLogger(_logger);
                        _deployAddon.SetTempDirectory(tempFolder);

                        _deployAddon.Deploy();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to deploy the Add-on service {assemblyName}: {ex.Message}.");
            }

        }

        private void UndeployAddon()
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().
                    SingleOrDefault(assembly => assembly.GetName().Name == WellKnownData.AddonServiceName);

                if (assembly == null)
                {
                    _logger.Error(
                        $"Failed to find the reference to the loaded Add-on {WellKnownData.AddonServiceName}.  Loading the Add-on.");
                    assembly = Assembly.LoadFrom(WellKnownData.AddonServicePath);
                }


                var undeployAddonClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass 
                                                                               && t.Name.Equals(WellKnownData.UndeployAddonClassName) 
                                                                               && typeof(IUndeployAddon).IsAssignableFrom(t));

                if (undeployAddonClass != null)
                {
                    _logger.Information($"Loading the {WellKnownData.UndeployAddonClassName} class.");
                    var undeployAddon = (IUndeployAddon) Activator.CreateInstance(undeployAddonClass);

                    if (undeployAddon == null)
                    {
                        _logger.Warning($"Unable to instantiate the Add-on service {WellKnownData.UndeployAddonClassName} class.");
                    }
                    else
                    {
                        _undeployAddon = undeployAddon;
                        _undeployAddon.SetLogger(_logger);
                        _undeployAddon.SetPluginDb(_configDb);

                        _undeployAddon.Undeploy();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to undeploy the Add-on service {WellKnownData.AddonServiceName}: {ex.Message}.");
            }

        }
    }
}
