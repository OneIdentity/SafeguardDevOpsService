using System.IO.Compression;
using Microsoft.AspNetCore.Http;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IAddonLogic
    {
        public void InstallAddon(IFormFile formFile);
        public void RemoveAddon();
    }
}
