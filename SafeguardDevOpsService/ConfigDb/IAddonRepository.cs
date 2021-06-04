using System.Collections.Generic;
using OneIdentity.DevOps.Common;

#pragma warning disable 1591

namespace OneIdentity.DevOps.ConfigDb
{
    public interface IAddOnRepository
    {
        IEnumerable<AddOnWithCredentials> GetAllAddOns();
        AddOnWithCredentials GetAddOnByName(string name);
        AddOnWithCredentials SaveAddOn(AddOnWithCredentials plugin);
        void DeleteAddOnByName(string name);
    }
}
