using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Common;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IAddonLogic
    {
        void InstallAddon(IFormFile formFile, bool force);
        void RemoveAddon(string name);
        IEnumerable<Addon> GetAddons();
        Addon GetAddon(string addonName);
    }
}
