using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
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


        public AddonLogic(IConfigurationRepository configDb)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
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

        private void InstallAddon(ZipArchive zipArchive)
        {
            var manifestEntry = zipArchive.GetEntry(WellKnownData.ManifestPattern);
            if (manifestEntry == null)
            {
                throw LogAndException("Failed to find the manifest for the add-on.");
            }

            var tempFolder = GetTemporaryDirectory();

            try
            {
                using (var reader = new StreamReader(manifestEntry.Open()))
                {
                    var manifest = reader.ReadToEnd();
                    var pluginManifest = JsonHelper.DeserializeObject<PluginManifest>(manifest);
                    if (pluginManifest != null)
                    {
                        RestartManager.Instance.ShouldRestart = true;
                        zipArchive.ExtractToDirectory(tempFolder, true);

                        DeployAddon(pluginManifest.Assembly, tempFolder);
                    }
                    else
                    {
                        throw LogAndException(
                            $"Add-on package does not contain a {WellKnownData.ManifestPattern} file.");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
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

        private string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

    }
}
