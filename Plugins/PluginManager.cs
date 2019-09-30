using System;
using System.Threading;
using System.Threading.Tasks;
using OneIdentity.SafeguardDevOpsService.ConfigDb;
using OneIdentity.SafeguardDevOpsService.Data;

namespace OneIdentity.SafeguardDevOpsService.Plugins
{
    public class PluginManager : IDisposable, IService
    {

        private TaskCompletionSource<bool> CompletionSource { get; } = new TaskCompletionSource<bool>();
        public string ServiceName => GetType().Name;

        private readonly IConfigurationRepository _configurationRepository;

        public PluginManager(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository;
        }

        public void Dispose()
        {
        }

        public Task RunAsync(CancellationToken token)
        {
            token.Register(() =>
            {
                Dispose();
                CompletionSource.TrySetCanceled(token);
            });

//TODO: Call something here.
            DetectPluginsAsync(token);

            return CompletionSource.Task;
        }

        private async Task DetectPluginsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {

                }
                catch (TaskCanceledException)
                {
                    /* ignore */
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "Error publishing the Sessions module score.");
                }
            }
        }
    }
}
