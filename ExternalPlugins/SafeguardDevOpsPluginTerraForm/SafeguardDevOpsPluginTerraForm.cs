using System;
using System.Collections.Generic;
using OneIdentity.Common;

namespace OneIdentity.SafeguardDevOpsPlugin.TerraForm
{
    public class TerraFormDevOpsPlugin : SafeguardDevOpsPluginBase
    {
        public TerraFormDevOpsPlugin():base()
        {
            
        }

        public override void ProcessPassword(string accountName, string password)
        {
            var URL = base.Configuration["Url"];
            base.BaseLog.Information($"Processing password for account {accountName}");
            //TODO: put code
        }

    }
}
