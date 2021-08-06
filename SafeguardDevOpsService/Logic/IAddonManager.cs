
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.Data;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IAddonManager
    {
        void Run();
        AddonStatus GetAddonStatus(Addon addon, bool isLicensed);
        void ShutdownAddon(Addon addon);
    }
}
