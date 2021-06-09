using System.Collections.Generic;
using OneIdentity.DevOps.Common;

#pragma warning disable 1591

namespace OneIdentity.DevOps.ConfigDb
{
    public interface IAddonRepository
    {
        IEnumerable<Addon> GetAllAddons();
        Addon GetAddonByName(string name);
        Addon SaveAddon(Addon plugin);
        void DeleteAddonByName(string name);
    }
}
