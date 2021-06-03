using System.Collections.Generic;
using OneIdentity.DevOps.Common;

#pragma warning disable 1591

namespace OneIdentity.DevOps.ConfigDb
{
    public interface IAddonRepository
    {
        IEnumerable<AddonWithCredentials> GetAllAddons();
        AddonWithCredentials GetAddonByName(string name);
        AddonWithCredentials SaveAddon(AddonWithCredentials plugin);
        void DeleteAddonByName(string name);
    }
}
