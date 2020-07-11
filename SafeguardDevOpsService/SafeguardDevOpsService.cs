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

#pragma warning disable 1591

namespace OneIdentity.DevOps
{
    public class SafeguardDevOpsService
    {
        private readonly IWebHost _host;
        private readonly IEnumerable<IPluginManager> _services;

        public SafeguardDevOpsService()
        {
            var webSslCert = CheckDefaultCertificate();

            if (webSslCert == null)
            {
                Serilog.Log.Logger.Error("Failed to find or change the default SSL certificate.");
                System.Environment.Exit(1);
            }

            var configuration = new ConfigurationBuilder()
                .AddJsonFile($"{WellKnownData.AppSettings}.json", optional: true, reloadOnChange: true).Build();
            var httpsPort = configuration["HttpsPort"] ?? "443";

            _host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    int port;
                    if (int.TryParse(httpsPort, out port) == false)
                        port = 443;
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

        private X509Certificate2 CheckDefaultCertificate()
        {
            using var db = new LiteDbConfigurationRepository();

            X509Certificate2 webSslCert = db.WebSslCertificate;
            if (webSslCert != null)
                return webSslCert;

            webSslCert = CertificateHelper.CreateDefaultSSLCertificate();
            db.WebSslCertificate = webSslCert;
            Serilog.Log.Logger.Information("Created and installed a default web ssl certificate.");

            // Need to make sure that we return a db instance of the certificate rather than a local instance
            //  So rather than just returning the webSslCert created above, get a new instance of the certificate
            //  from the database.
            return db.WebSslCertificate;
        }
    }
}
