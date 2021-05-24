using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IAddonService
    {
        string Name { get; }
        string DisplayName { get; }
        string Description { get; }
        Task RunAddonServiceAsync(CancellationToken cancellationToken);
        void SetLogger(ILogger logger);
        void SetDatabase(ISettingsRepository settingDb);
        void Unload();
    }
}
