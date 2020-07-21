using System;
using System.Collections.Generic;
using System.IO;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using OneIdentity.DevOps.Logic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using OneIdentity.DevOps.ConfigDb;
using Serilog;

namespace OneIdentity.DevOps
{
    internal class SafeguardDevOpsService
    {
        private readonly IWebHost _host;
        private readonly IEnumerable<IPluginManager> _services;

        public SafeguardDevOpsService()
        {
            CheckGenerateUniqueIdentifier();
            var webSslCert = CheckDefaultCertificate();

            if (webSslCert == null)
            {
                Log.Logger.Error("Failed to find or change the default SSL certificate.");
                Environment.Exit(1);
            }

            Log.Logger.Information($"Configuration file location: {Path.Combine(WellKnownData.ServiceDirPath, WellKnownData.AppSettings)}.json");
            var configuration = new ConfigurationBuilder()
                .AddJsonFile($"{Path.Combine(WellKnownData.ServiceDirPath, WellKnownData.AppSettings)}.json",
                    optional: true, reloadOnChange: true).Build();
            var httpsPort = configuration["HttpsPort"] ?? "443";

            _host = new WebHostBuilder()
                .UseSerilog()
                .UseKestrel(options =>
                {
                    if (int.TryParse(httpsPort, out var port) == false)
                        port = 443;
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

            // TODO: better way to start this service??
            _services = (IEnumerable<IPluginManager>)_host.Services.GetService(typeof(IEnumerable<IPluginManager>));
        }

        public void Start()
        {
            _services.ToList().ForEach(s => s.Run());
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

            if (svcId == null)
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

            webSslCert = CertificateHelper.CreateDefaultSSLCertificate();
            db.WebSslCertificate = webSslCert;
            Log.Logger.Information("Created and installed a default web ssl certificate.");

            // Need to make sure that we return a db instance of the certificate rather than a local instance
            //  So rather than just returning the webSslCert created above, get a new instance of the certificate
            //  from the database.
            return db.WebSslCertificate;
        }
    }
}
