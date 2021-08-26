using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Common;
using OneIdentity.DevOps.Data;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IAddonLogic
    {
        void InstallAddon(string base64Addon, bool force);
        void InstallAddon(IFormFile formFile, bool force);
        void RemoveAddon(string name);
        IEnumerable<Addon> GetAddons();
        Addon GetAddon(string addonName);
        AddonStatus GetAddonStatus(string addonName);
        void ConfigureDevOpsAddOn(string addonName);
        void RestartDevOpsAddOn(string addonName);
    }
}
