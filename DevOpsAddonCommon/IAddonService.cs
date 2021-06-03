using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IAddonService
    {
        string Name { get; set; }
        string DisplayName { get; set; }
        string Description { get; set; }
        AddonWithCredentials AddOn { get; set; }
        Task RunAddonServiceAsync(CancellationToken cancellationToken);
        void SetLogger(ILogger logger);
//        void SetDatabase(object configDb);
        void Unload();
    }
}
