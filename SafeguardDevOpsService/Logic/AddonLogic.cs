using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    internal class AddOnLogic : IAddOnLogic
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private IDeployAddOn _deployAddOn;
        private IUndeployAddOn _undeployAddOn;


        public AddOnLogic(IConfigurationRepository configDb)
        {
            _logger = Serilog.Log.Logger;
            _configDb = configDb;
        }

        private DevOpsException LogAndException(string msg, Exception ex = null)
        {
            _logger.Error(msg);
            return new DevOpsException(msg, ex);
        }


        public void InstallAddOn(IFormFile formFile, bool force)
        {
            if (formFile.Length <= 0)
                throw LogAndException("Add-on cannot be null or empty");

            try
            {
                using (var inputStream = formFile.OpenReadStream())
                using (var zipArchive = new ZipArchive(inputStream, ZipArchiveMode.Read))
                {
                    InstallAddOn(zipArchive, force);
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to install the Add-on plugin. {ex.Message}");
            }
        }

        public void RemoveAddOn(string name)
        {
            var addOn = _configDb.GetAddOnByName(name);
            if (addOn == null)
            {
                throw LogAndException("Failed to find the add-on in the database.");
            }

            _logger.Information("Saving the remove token to disk. The Add-on will be delete after a reboot.");
            try
            {
                using (File.Create(Path.Combine(WellKnownData.ProgramDataPath, addOn.Manifest.DestinationFolder, WellKnownData.AddOnDeleteFile))) {}
            }
            catch {}

            UndeployAddOn(addOn);
        }

        public IEnumerable<AddOn> GetAddOns()
        {
            var addOnsInternal = _configDb.GetAllAddOns();
            var addOns = addOnsInternal.Select(x => JsonHelper.DeserializeObject<AddOn>(JsonHelper.SerializeObject(x)));
            return addOns;
        }

        public AddOn GetAddOn(string addOnName)
        {
            var addOnInternal = _configDb.GetAddOnByName(addOnName);
            var addOn = JsonHelper.DeserializeObject<AddOn>(JsonHelper.SerializeObject(addOnInternal));
            return addOn;
        }

        private void InstallAddOn(ZipArchive zipArchive, bool force)
        {
            var manifestEntry = zipArchive.GetEntry(WellKnownData.ManifestPattern);
            if (manifestEntry == null)
            {
                throw LogAndException("Failed to find the manifest for the add-on.");
            }

            using (var reader = new StreamReader(manifestEntry.Open()))
            {
                var manifest = reader.ReadToEnd();
                var addOnManifest = JsonHelper.DeserializeObject<AddOnManifest>(manifest);
                if (addOnManifest != null)
                {
                    var addOn = _configDb.GetAddOnByName(addOnManifest.Name);
                    if (addOn != null)
                    {
                        if (force)
                        {
                            _configDb.DeleteAddOnByName(addOnManifest.Name);
                        }
                        else
                        {
                            _logger.Warning($"Add-on {addOn.Name} already exists. ");
                            return;
                        }
                    }

                    RestartManager.Instance.ShouldRestart = true;
                    zipArchive.ExtractToDirectory(WellKnownData.AddOnServiceStageDirPath, true);

                    addOn = new AddOnWithCredentials()
                    {
                        Name = addOnManifest.Name,
                        Manifest = addOnManifest,
                    };
                    _configDb.SaveAddOn(addOn);

                    DeployAddOn(addOnManifest, WellKnownData.AddOnServiceStageDirPath);
                }
                else
                {
                    throw LogAndException(
                        $"Add-on package does not contain a {WellKnownData.ManifestPattern} file.");
                }
            }
        }

        private void DeployAddOn(AddOnManifest addOnManifest, string tempFolder)
        {
            try
            {
                var addOnPath = Path.Combine(WellKnownData.AddOnServiceStageDirPath, addOnManifest.SourceFolder, addOnManifest.Assembly);
                if (!File.Exists(addOnPath))
                {
                    throw LogAndException("Failed to find the add-on module.");
                }

                var assembly = Assembly.LoadFrom(addOnPath);

                var deployAddOnClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass 
                                                                   && t.Name.Equals(addOnManifest.DeployClassName) 
                                                                   && typeof(IDeployAddOn).IsAssignableFrom(t));

                if (deployAddOnClass != null)
                {
                    _logger.Information($"Loading the Add-on service from path {addOnPath}.");
                    var deployAddOn = (IDeployAddOn) Activator.CreateInstance(deployAddOnClass);

                    if (deployAddOn == null)
                    {
                        _logger.Warning($"Unable to instantiate the Add-on service from {addOnPath}");
                    }
                    else
                    {
                        _deployAddOn = deployAddOn;
                        _deployAddOn.SetLogger(_logger);
                        _deployAddOn.SetTempDirectory(tempFolder);

                        _deployAddOn.Deploy(addOnManifest, _configDb.GetWebSslPemCertificate());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to deploy the Add-on service {addOnManifest.Name}: {ex.Message}.");
            }

        }

        private void UndeployAddOn(AddOnWithCredentials addOn)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().
                    SingleOrDefault(assembly => assembly.GetName().Name == addOn.Manifest.AssemblyName);

                if (assembly == null)
                {
                    _logger.Warning(
                        $"Failed to find the reference to the loaded Add-on {addOn.Manifest.AssemblyName}.  Loading the Add-on.");
                    assembly = Assembly.LoadFrom(Path.Combine(WellKnownData.ProgramDataPath, addOn.Manifest.DestinationFolder, addOn.Manifest.Assembly));
                }


                var undeployAddOnClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass 
                                                                               && t.Name.Equals(addOn.Manifest.UndeployClassName) 
                                                                               && typeof(IUndeployAddOn).IsAssignableFrom(t));

                if (undeployAddOnClass != null)
                {
                    _logger.Information($"Loading the {addOn.Manifest.UndeployClassName} class.");
                    var undeployAddOn = (IUndeployAddOn) Activator.CreateInstance(undeployAddOnClass);

                    if (undeployAddOn == null)
                    {
                        _logger.Warning($"Unable to instantiate the Add-on service {addOn.Manifest.UndeployClassName} class.");
                    }
                    else
                    {
                        _undeployAddOn = undeployAddOn;
                        _undeployAddOn.SetLogger(_logger);

                        _undeployAddOn.Undeploy(addOn.Manifest);
                        _configDb.DeleteAddOnByName(addOn.Manifest.Name);
                        _configDb.DeletePluginByName(addOn.Manifest.PluginName);
                    }
                }
            }
            catch (Exception ex)
            {
                throw LogAndException($"Failed to undeploy the Add-on service {addOn.Manifest.ServiceClassName}: {ex.Message}.");
            }
        }
    }
}
