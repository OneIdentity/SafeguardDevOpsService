using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IDevOpsWatchdog
    {
        string Name { get; }
        string DisplayName { get; }
        string Description { get; }
        Task RunWatchdogAsync(CancellationToken cancellationToken);
        void SetLogger(ILogger logger);
        void SetDatabase(ISettingsRepository settingDb);
        void Unload();
    }
}
