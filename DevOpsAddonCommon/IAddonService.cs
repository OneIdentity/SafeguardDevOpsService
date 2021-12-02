using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic.CompilerServices;
using Serilog;

namespace OneIdentity.DevOps.Common
{
    public interface IAddonService
    {
        string Name { get; set; }
        string DisplayName { get; set; }
        string Description { get; set; }
        ILogger Logger { get; set; }
        Tuple<string,string> TlsCertificates { get; set; }
        Tuple<bool, List<string>> GetHealthStatus();
        Addon AddOn { get; set; }
        Task RunAddonServiceAsync(CancellationToken cancellationToken);
        void Unload();
    }
}
