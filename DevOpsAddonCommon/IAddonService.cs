using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IAddOnService
    {
        string Name { get; set; }
        string DisplayName { get; set; }
        string Description { get; set; }
        AddOnWithCredentials AddOn { get; set; }
        Task RunAddOnServiceAsync(CancellationToken cancellationToken);
        void SetLogger(ILogger logger);
//        void SetDatabase(object configDb);
        void Unload();
    }
}
