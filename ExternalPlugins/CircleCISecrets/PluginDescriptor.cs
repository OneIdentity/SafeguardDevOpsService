using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneIdentity.DevOps.Common;
using RestSharp;
using Serilog;

namespace OneIdentity.DevOps.CircleCISecrets
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private VaultConnection _secretsClient;
        private Dictionary<string,string> _configuration;
        private Regex _rgx;
        private ContextItem _contextItem = null;
        private string _vcsType = null;
        private string _vcsOrganization = null;
        private string _vcsProject = null;
        private string _vcsSlug = null;

        private const string _address = "http://circleci.com/api/v2";
        private const string OrganizationId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
        private const string OrganizationIdName = "Organization Id";
        private const string ContextName = "Context Name";
        private const string RepositoryUrlName = "Repository Url";

        public string Name => "CircleCISecrets";
        public string DisplayName => "CircleCI Secrets";
        public string Description => "This is the CircleCI Secrets plugin for updating passwords";
        public bool SupportsReverseFlow => false;  // CircleCI only allows an outside caller to get a masked credential. 

        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { OrganizationIdName, OrganizationId },
                { ContextName, "" },
                { RepositoryUrlName, "" }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null 
                && ((configuration.ContainsKey(OrganizationIdName) && configuration.ContainsKey(ContextName)) 
                    || configuration.ContainsKey(RepositoryUrlName)))
            {
                _configuration = configuration;
                Logger.Information($"Plugin {Name} has been successfully configured.");
                _rgx = new Regex("[^a-zA-Z0-9_]");

                if (configuration.ContainsKey(RepositoryUrlName) && !string.IsNullOrEmpty(configuration[RepositoryUrlName]))
                {
                    var uri = new Uri(configuration[RepositoryUrlName]);
                    if (uri.Host.Contains("github", StringComparison.OrdinalIgnoreCase))
                    {
                        _vcsType = "gh";
                    }
                    else if (uri.Host.Contains("bitbucket", StringComparison.OrdinalIgnoreCase))
                    {
                        _vcsType = "bb";
                    }

                    var segments = uri.AbsolutePath.Split('/');
                    if (segments.Length >= 3)
                    {
                        _vcsOrganization = segments[1];
                        _vcsProject = segments[2];
                    }
                    else
                    {
                        _vcsType = null;
                        _vcsSlug = null;
                        _vcsProject = null;
                        _vcsOrganization = null;
                    }
                }
                else
                {
                    _vcsType = null;
                    _vcsSlug = null;
                    _vcsProject = null;
                    _vcsOrganization = null;
                }
            }
            else
            {
                Logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            if (_configuration != null && credential != null)
            {
                try
                {
                    _secretsClient = new VaultConnection(_address, credential, Logger);
                    Logger.Information($"Plugin {Name} successfully authenticated.");
                }
                catch (Exception ex)
                {
                    Logger.Information(ex, $"Invalid configuration for {Name}. Please use the api to set a valid configuration. {ex.Message}");
                }

                if (_configuration[OrganizationIdName] != null)
                {
                    try
                    {
                        var response = _secretsClient.InvokeMethodFull(Method.Get, $"/context?owner-id={_configuration[OrganizationIdName]}");
                        var contexts = JsonHelper.DeserializeObject<ContextItems>(response.Body);
                        if (contexts?.items != null)
                        {
                            _contextItem = contexts.items.ToArray()
                                .FirstOrDefault(x => x.name.Equals(_configuration[ContextName]), null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
                    }
                }

                if (_vcsType != null && _vcsOrganization != null && _vcsProject != null)
                {
                    try
                    {
                        var response = _secretsClient.InvokeMethodFull(Method.Get, $"/project/{_vcsType}/{_vcsOrganization}/{_vcsProject}");
                        var project = JsonHelper.DeserializeObject<ProjectItem>(response.Body);
                        _vcsSlug = project?.slug;
                    }
                    catch (Exception ex)
                    {
                        _vcsSlug = null;
                        Logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
                    }
                }
            }
            else
            {
                Logger.Error("The plugin configuration or credential is missing.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_secretsClient == null)
                return false;

            if (_contextItem == null && _vcsSlug == null)
            {
                Logger.Information($"Organization context {_configuration[ContextName] ?? "undefined"} not found for {DisplayName} or project not found for {_configuration[RepositoryUrlName] ?? "undefined"}.");
                return false;
            }

            // If we got a context or a slug, then we were able to connect using the credentials during the
            //  SetVaultCredential() callback above.
            return true;
        }

        public string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName)
        {
            ValidationHelper.CanReverseFlow(this);
            return null;
        }

        public string SetCredential(CredentialType credentialType, string asset, string account, string[] credential, string altAccountName)
        {
            switch (credentialType)
            {
                case CredentialType.Password:
                    return SetPassword(asset, account, credential, altAccountName);
                case CredentialType.SshKey:
                    return SetSshKey(asset, account, credential, altAccountName);
                case CredentialType.ApiKey:
                    return SetApiKey(asset, account, credential, altAccountName);
                default:
                    Logger.Error($"Invalid credential type sent to the {DisplayName} plugin instance.");
                    break;
            }

            return null;
        }

        public void Unload()
        {
            Logger = null;
            _secretsClient = null;
            _configuration.Clear();
            _configuration = null;
        }

        private string SetPassword(string asset, string account, string[] password, string altAccountName)
        {
            if (!ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            if (_configuration == null || _secretsClient == null || (_contextItem == null && _vcsSlug == null))
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            if (password is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}_{account}", "_");

            return StoreCredential(name, "{\"value\":\""+password[0]+"\"}", "{\"name\":\""+name+"\",\"value\":\""+password[0]+"\"}") ? password[0] : null;
        }

        private string SetSshKey(string asset, string account, string[] sshKey, string altAccountName)
        {
            if (!ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            if (_configuration == null || _secretsClient == null || (_contextItem == null && _vcsSlug == null))
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            if (sshKey is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}_{account}", "_") + "_sshkey";

            return StoreCredential(name, "{\"value\":\""+sshKey[0]+"\"}", "{\"name\":\""+name+"\",\"value\":\""+sshKey[0]+"\"}") ? sshKey[0] : null;
        }

        private string SetApiKey(string asset, string account, string[] apiKeys, string altAccountName)
        {
            if (!ValidationHelper.CanHandleApiKey(this))
            {
                return null;
            }

            if (_configuration == null || _secretsClient == null || (_contextItem == null && _vcsSlug == null))
            {
                Logger.Error($"No vault connection. Make sure that the {DisplayName} plugin has been configured.");
                return null;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}_{account}", "_");
            var retval = true;

            foreach (var apiKeyJson in apiKeys)
            {
                var apiKey = JsonHelper.DeserializeObject<ApiKey>(apiKeyJson);
                if (apiKey != null)
                {
                    var n = $"{name}_{_rgx.Replace(apiKey.Name, "_")}";
                    var k = $"{apiKey.ClientId}:{apiKey.ClientSecret}";
                    StoreCredential(n, "{\"value\":\""+k+"\"}", "{\"name\":\""+n+"\",\"value\":\""+k+"\"}");
                }
                else
                {
                    Logger.Error($"The ApiKey {name} {apiKey.ClientId} failed to save to the {this.DisplayName} vault.");
                    retval = false;
                }
            }

            return retval ? "" : null;
        }

        private bool StoreCredential(string name, string contextPayload, string projectPayload)
        {
            var retval = true;

            if (_contextItem != null)
            {
                try
                {
                    var response = _secretsClient.InvokeMethodFull(Method.Put, $"/context/{_contextItem.id}/environment-variable/{name}", contextPayload);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Logger.Information($"The secret for {name} has been successfully stored in the context.");
                    }
                    else
                    {
                        Logger.Error($"Failed to set the context secret for {name}: {response.Body}.");
                        retval = false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to set the context secret for {name}: {ex.Message}.");
                    retval = false;
                }
            }

            if (_vcsSlug != null)
            {
                try
                {
                    var response = _secretsClient.InvokeMethodFull(Method.Post, $"/project/{_vcsSlug}/envvar", projectPayload);

                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                    {
                        Logger.Information($"The secret for {name} has been successfully stored in the project environment.");
                    }
                    else
                    {
                        Logger.Error($"Failed to set the project environment secret for {name}: {response.Body}.");
                        retval = false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to set the project environment secret for {name}: {ex.Message}.");
                    retval = false;
                }
            }

            return retval;
        }

        class ContextItems
        {
            public List<ContextItem> items { get; set; }
            public string next_page_token { get; set; }
        }

        class ContextItem
        {
            public string id { get; set; }
            public string name { get; set; }
        }

        class ProjectItem
        {
            public string slug { get; set; }
            public string name { get; set; }
            public string id { get; set; }
            public string organization_name { get; set; }
            public string organization_slug { get; set; }
            public string organization_id { get; set; }
            public VcsInfo vcs_info { get; set; }
        }

        class VcsInfo
        {
            public string vcs_url { get; set; }
            public string provider { get; set; }
            public string default_branch { get; set; }
        }
    }
}
