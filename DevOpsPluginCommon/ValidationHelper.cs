using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneIdentity.DevOps.Common
{
    public class ValidationHelper
    {
        public static bool CanReverseFlow(ILoadablePlugin plugin)
        {
            if (!plugin.SupportsReverseFlow || !plugin.ReverseFlowEnabled)
            {
                plugin.Logger.Error($"The {plugin.DisplayName} plugin does not support reverse flow or reverse flow is not enabled.");
                return false;
            }

            return true;
        }

        public static bool CanHandlePassword(ILoadablePlugin plugin)
        {
            if (plugin.AssignedCredentialType != CredentialType.Password)
            {
                plugin.Logger.Error($"The {plugin.DisplayName} plugin instance does not handle the Password credential type.");
                return false;
            }

            return true;
        }

        public static bool CanHandleSshKey(ILoadablePlugin plugin)
        {
            if (plugin.AssignedCredentialType != CredentialType.SshKey)
            {
                plugin.Logger.Error($"The {plugin.DisplayName} plugin instance does not handle the SshKey credential type.");
                return false;
            }

            return true;
        }

        public static bool CanHandleApiKey(ILoadablePlugin plugin)
        {
            if (plugin.AssignedCredentialType != CredentialType.ApiKey)
            {
                plugin.Logger.Error($"The {plugin.DisplayName} plugin instance does not handle the ApiKey credential type.");
                return false;
            }

            return true;
        }

    }
}
