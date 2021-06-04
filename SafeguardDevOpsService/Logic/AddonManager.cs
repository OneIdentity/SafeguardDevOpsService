using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.ConfigDb;

namespace OneIdentity.DevOps.Logic
{
    internal class AddOnManager : IHostedService, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfigurationRepository _configDb;
        private readonly ISafeguardLogic _safeguardLogic;

        private IAddOnService _devOpsAddOn;

        public AddOnManager(IConfigurationRepository configDb, ISafeguardLogic safeguardLogic)
        {
            _configDb = configDb;
            _logger = Serilog.Log.Logger;
            _safeguardLogic = safeguardLogic;
        }

        public void Dispose()
        {

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var files = Directory.GetFiles(WellKnownData.ProgramDataPath, WellKnownData.AddOnDeleteFile, SearchOption.AllDirectories);
            if (files.Any())
            {
                foreach (var addOnPath in files)
                {
                    Directory.Delete(Path.GetDirectoryName(addOnPath), true);
                }
            }
            else
            {
                var addOns = _configDb.GetAllAddOns();

                foreach (var addOn in addOns)
                {
                    if (LoadAddOnService(addOn))
                    {
                        Task.Run(async () => await _devOpsAddOn.RunAddOnServiceAsync(cancellationToken),
                            cancellationToken);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private bool LoadAddOnService(AddOnWithCredentials addOn)
        {
            var addOnAssemblyPath = Path.Combine(WellKnownData.ProgramDataPath, addOn.Manifest.DestinationFolder, addOn.Manifest.Assembly);

            try
            {
                if (!File.Exists(addOnAssemblyPath))
                    return false;

                var assembly = Assembly.LoadFrom(addOnAssemblyPath);

                var addOnClass = assembly.GetTypes().FirstOrDefault(t => t.IsClass 
                                                                   && t.Name.Equals(addOn.Manifest.ServiceClassName) 
                                                                   && typeof(IAddOnService).IsAssignableFrom(t));

                if (addOnClass != null)
                {
                    _logger.Information($"Loading Add-on service from path {addOnAssemblyPath}.");
                    var addOnService = (IAddOnService) Activator.CreateInstance(addOnClass);

                    if (addOnService == null)
                    {
                        _logger.Error($"Unable to instantiate the Add-on service from {addOnAssemblyPath}");
                    }
                    else
                    {
                        _devOpsAddOn = addOnService;
                        _devOpsAddOn.SetLogger(_logger);

                        _devOpsAddOn.Name = addOn.Manifest.Name;
                        _devOpsAddOn.DisplayName = addOn.Manifest.DisplayName;
                        _devOpsAddOn.Description = addOn.Manifest.Description;
                        _devOpsAddOn.AddOn = addOn;

                        //Subscribe for property changes in the addOn object
                        _devOpsAddOn.AddOn.PropertyChanged += AddOnPropertyChangedHandler;

                        _logger.Information($"Successfully loaded the Add-on Service {_devOpsAddOn.DisplayName} : {_devOpsAddOn.Description}.");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load the add-on service {addOn.Manifest.Name}: {ex.Message}.");
            }

            return false;
        }

        public void AddOnPropertyChangedHandler(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _logger.Information($"Property {e.PropertyName} just changed");

            _devOpsAddOn.AddOn.PropertyChanged -= AddOnPropertyChangedHandler;
            _devOpsAddOn.AddOn.IsDirty = false;
            _devOpsAddOn.AddOn.PropertyChanged += AddOnPropertyChangedHandler;

        }

    }
}
