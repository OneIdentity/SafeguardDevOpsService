using System;
using System.Collections.Generic;
using System.IO;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using OneIdentity.DevOps.Logic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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

            _host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.ListenAnyIP(443, listenOptions =>
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
            if (webSslCert == null)
            {
                webSslCert = CertificateHelper.CreateDefaultSSLCertificate();
                db.WebSslCertificate = webSslCert;
                Serilog.Log.Logger.Error("Created and installed a default web ssl certificate.");
            }

            return webSslCert;
        }
    }
}
