using System;
using System.Collections.Generic;
using OneIdentity.Common;

namespace OneIdentity.SafeguardDevOpsPlugin.TerraForm
{
    public class TerraFormDevOpsPlugin : SafeguardDevOpsPluginBase
    {
        public TerraFormDevOpsPlugin(Dictionary<string, string> configuration):base(configuration)
        {
            if(configuration == null || configuration.Count == 0)
            {
                base.BaseLog.Error("Configuration is empty");
                throw new Exception("Need to provide configuration");
            }
        }

        public override void ProcessPassword(string accountName, string password)
        {
            var URL = base.Configuration["Url"];
            base.BaseLog.Information($"Processing password for account {accountName}");
            //TODO: put code
        }

    }
}
