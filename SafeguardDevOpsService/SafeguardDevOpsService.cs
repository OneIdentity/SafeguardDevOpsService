using System;
using System.IO;
using System.Net;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using OneIdentity.DevOps.Logic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using OneIdentity.DevOps.ConfigDb;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Extensions;
using Serilog;
using Serilog.Events;

namespace OneIdentity.DevOps
{
    internal class SafeguardDevOpsService
    {
        private readonly IWebHost _host;
        private readonly IPluginManager _pluginManager;
        private readonly IAddonManager _addonManager;
        private readonly IMonitoringLogic _monitoringLogic;

        public SafeguardDevOpsService()
        {
            CheckGenerateUniqueIdentifier();
            var webSslCert = CheckDefaultCertificate();

            if (webSslCert == null)
            {
                Log.Logger.Error("Failed to find or change the default SSL certificate.");
                Environment.Exit(1);
            }

            if (bool.Parse(Environment.GetEnvironmentVariable("DOCKER_RUNNING") ?? "false"))
            {
                Log.Logger.Information("Running in Docker container");
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST_IP")))
                {
                    var hostEntry = Dns.GetHostEntry("host.docker.internal");
                    Log.Logger.Information($"Using host.docker.internal IP: {hostEntry.AddressList[0]}");
                    Environment.SetEnvironmentVariable("DOCKER_HOST_IP", hostEntry.AddressList[0].ToString());
                }
                Log.Logger.Information($"Docker host IP: {Environment.GetEnvironmentVariable("DOCKER_HOST_IP")}");
            }

            Log.Logger.Information($"Thumbprint for {webSslCert.Subject}: {webSslCert.Thumbprint}");
            Log.Logger.Information(webSslCert.ToPemFormat());

            Log.Logger.Information($"Configuration file location: {Path.Combine(WellKnownData.ServiceDirPath, WellKnownData.AppSettings)}.json");
            var configuration = new ConfigurationBuilder()
                .AddJsonFile($"{Path.Combine(WellKnownData.ServiceDirPath, WellKnownData.AppSettings)}.json",
                    optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            var httpsPort = configuration["HttpsPort"] ?? WellKnownData.DefaultServicePort;
            var logLevel = configuration["LogLevel"];

            if (logLevel != null)
            {
                if (Enum.TryParse(logLevel, out DevOpsLogLevel level))
                {
                    var logLevelSwitch = LogLevelSwitcher.Instance.LogLevelSwitch;

                    switch (level)
                    {
                        case DevOpsLogLevel.Information:
                            logLevelSwitch.MinimumLevel = LogEventLevel.Information;
                            break;

                        case DevOpsLogLevel.Debug:
                            logLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                            break;
                        case DevOpsLogLevel.Error:
                            logLevelSwitch.MinimumLevel = LogEventLevel.Error;
                            break;
                        case DevOpsLogLevel.Warning:
                            logLevelSwitch.MinimumLevel = LogEventLevel.Warning;
                            break;
                        case DevOpsLogLevel.Fatal:
                            logLevelSwitch.MinimumLevel = LogEventLevel.Fatal;
                            break;
                        case DevOpsLogLevel.Verbose:
                            logLevelSwitch.MinimumLevel = LogEventLevel.Verbose;
                            break;
                    }
                }
                else
                {
                    Log.Logger.Error($"{logLevel} is not not a recognized log level. Continuing to use the default log level.");
                }
            }


            _host = new WebHostBuilder()
                .UseSerilog()
                .UseKestrel(options =>
                {
                    if (int.TryParse(httpsPort, out var port) == false)
                    {
                        Log.Logger.Warning($"Failed to parse HttpsPort from appsettings.json '{httpsPort}'");
                        port = int.Parse(WellKnownData.DefaultServicePort);
                    }
                    Log.Logger.Information($"Binding web server to port: {port}.");
                    options.ListenAnyIP(port, listenOptions =>
                        {
                            listenOptions.UseHttps(webSslCert);
                        });
                })
                .ConfigureServices(services => services.AddAutofac())
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            _monitoringLogic = (IMonitoringLogic) _host.Services.GetService(typeof(IMonitoringLogic));
            _pluginManager = (IPluginManager)_host.Services.GetService(typeof(IPluginManager));
            _addonManager = (IAddonManager)_host.Services.GetService(typeof(IAddonManager));
        }

        public void Start()
        {
            // This kicks off a function that
            // checks for and loads any staged plugins
            _pluginManager.Run();
            // This kicks off a function that
            // checks for and loads any staged addons
            _addonManager.Run();
            // This kicks off a function that restores
            // the password change monitor to last known
            // running state.
            _monitoringLogic.Run();
            _host.RunAsync();
        }

        public void Stop()
        {
            _host.StopAsync().Wait();
        }

        private void CheckGenerateUniqueIdentifier()
        {
            Log.Logger.Information($"Service instance identifier file location: {WellKnownData.SvcIdPath}");

            string svcId = null;
            try
            {
                if (File.Exists(WellKnownData.SvcIdPath))
                {
                    svcId = File.ReadAllText(WellKnownData.SvcIdPath);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Information($"Failed to read the service instance identifier: {WellKnownData.SvcIdPath}", ex);
            }

            if (svcId == null || svcId.Equals(WellKnownData.ServiceIdentitifierRegenerate, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var random = new Random((int) DateTime.Now.Ticks);
                    const string pool = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                    var chars = Enumerable.Range(0, 10).Select(x => pool[random.Next(0, pool.Length)]);
                    var id = new string(chars.ToArray());

                    File.WriteAllText(WellKnownData.SvcIdPath, id);
                    svcId = id;
                }
                catch (Exception ex)
                {
                    Log.Logger.Information($"Failed to generate the service instance identifier: {WellKnownData.SvcIdPath}", ex);
                }
            }

            if (svcId == null)
            {
                Environment.Exit(1);
            }
        }

        private X509Certificate2 CheckDefaultCertificate()
        {
            using var db = new LiteDbConfigurationRepository();

            var webSslCert = db.WebSslCertificate;
            if (webSslCert != null)
                return webSslCert;

            webSslCert = CertificateHelper.CreateDefaultSslCertificate();
            db.WebSslCertificate = webSslCert;
            Log.Logger.Information("Created and installed a default web ssl certificate.");

            // Need to make sure that we return a db instance of the certificate rather than a local instance
            //  So rather than just returning the webSslCert created above, get a new instance of the certificate
            //  from the database.
            return db.WebSslCertificate;
        }
    }
}
