using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using OneIdentity.DevOps.Common;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IAddOnLogic
    {
        void InstallAddOn(IFormFile formFile, bool force);
        void RemoveAddOn(string name);
        IEnumerable<AddOn> GetAddOns();
        AddOn GetAddOn(string addOnName);
    }
}
