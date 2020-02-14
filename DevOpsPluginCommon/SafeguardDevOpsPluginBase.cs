using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public class SafeguardDevOpsPluginBase
    {
        private Dictionary<string, string> _configuration;
        private Serilog.Core.Logger _baseLog;

        public SafeguardDevOpsPluginBase()
        {
            SetLog();
        }

        public Dictionary<string, string> Configuration
        {
            get => _configuration;
            set => _configuration = value;
        }

        public Serilog.Core.Logger BaseLog => _baseLog;

        public virtual void ProcessPassword(string accountName, string password)
        {
            throw new Exception("ProcessPassword method needs to be implemented in the derived class");
        }

        /// <summary>
        /// Initializes configuration for populating ConfigDB in the DevOps service.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> InitializeConfiguration()
        {
            var baseConfig = new Dictionary<string, string>
            {
                {"Name", ""},
                {"Url", ""},
                {"Description", ""},
                {"Username", ""},
                {"Password", ""},
                {"LogLevel", "Possible values: 'Debug', 'Warning', 'Error'"},
                {"IsConfigured", "false"}
            };

            return baseConfig;
        }

        /// <summary>
        /// Initialize configuration with a provided JSON file.
        /// If a file doesn't exist - will do a basic initialization.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public Dictionary<string, string> InitializeConfiguration(string filePath)
        {
            Dictionary<string, string> baseConfig = null;

            if (File.Exists(filePath))
            {
                var strData = File.ReadAllText(filePath);

                try
                {
                    baseConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(strData);
                }
                catch (Exception)
                {
                    //throw;
                }
            }

            return baseConfig ?? InitializeConfiguration();
        }



        private void SetLog()
        {
            var logLevel = "Error"; //ConfigurationManager.AppSettings["LogLevel"];

            if (_configuration != null && _configuration.ContainsKey("LogLevel"))
            {
                logLevel = _configuration["LogLevel"];
            }

            var loggingDirectory = "Logs";

            if (!Path.IsPathRooted(loggingDirectory))
                loggingDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? throw new InvalidOperationException(), loggingDirectory);

            if (!Directory.Exists(loggingDirectory))
            {
                Directory.CreateDirectory(loggingDirectory);
            }

            var assemblyName = GetAssemblyName();

            assemblyName = Path.GetFileNameWithoutExtension(assemblyName);

            var lc = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(loggingDirectory,
                    $"{assemblyName}-{DateTime.Now:yyyy-MM-dd--HH-mm-ss}.log"), fileSizeLimitBytes: 100_000_000);

            //.WriteTo.File($".\\Logs\\SafePassageMigration-{DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")}.log",
            //                          fileSizeLimitBytes: 100_000_000,
            //                          rollOnFileSizeLimit: true);
            //.WriteTo.File(@".\Logs\SafePassageMigration_{Date}.log", retainedFileCountLimit: 7, flushToDiskInterval: new System.TimeSpan(0, 0, 30))
            //

            lc = lc.MinimumLevel.Information();

            if (logLevel.Equals("Debug", StringComparison.InvariantCultureIgnoreCase))
            {
                lc = lc.MinimumLevel.Debug();
            }
            else if (logLevel.Equals("Warning", StringComparison.InvariantCultureIgnoreCase))
            {
                lc = lc.MinimumLevel.Warning();
            }
            else if (logLevel.Equals("Error", StringComparison.InvariantCultureIgnoreCase))
            {
                lc = lc.MinimumLevel.Error();
            }


            _baseLog = lc.CreateLogger();
        }

        private string GetAssemblyName()
        {
            var currentAssembly = Assembly.GetExecutingAssembly();

            var callerAssemblies = (new StackTrace().GetFrames() ?? throw new InvalidOperationException())
                        .Select(x => x.GetMethod().ReflectedType?.Assembly).Distinct()
                        .Where(x => x.GetReferencedAssemblies().Any(y => y.FullName == currentAssembly.FullName));

            var initialAssembly = callerAssemblies.First();

            return initialAssembly.Location;
        }
    }
}
