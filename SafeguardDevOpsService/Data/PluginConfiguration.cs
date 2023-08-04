
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OneIdentity.DevOps.Common;

#pragma warning disable 1591

namespace OneIdentity.DevOps.Data
{
    public class PluginConfiguration
    {
        /// <summary>
        /// Third-party vault connection configuration.
        /// </summary>
        public Dictionary<string,string> Configuration { get; set; }

        /// <summary>
        /// Set the credential type handled by the plugin.
        /// Valid values are Password, ApiKey and SshKey.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Unknown;

        /// <summary>
        /// Current reverse flow state of the plugin.
        /// </summary>
        public bool ReverseFlowEnabled { get; set; } = false;
    }
}
