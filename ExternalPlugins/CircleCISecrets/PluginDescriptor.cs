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
        private ILogger _logger;

        private const string _address = "http://circleci.com/api/v2";
        private const string OrganizationId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
        private const string OrganizationIdName = "Organization Id";
        private const string ContextName = "Context Name";
        private const string RepositoryUrlName = "Repository Url";

        public string Name => "CircleCISecrets";
        public string DisplayName => "CircleCI Secrets";
        public string Description => "This is the CircleCI Secrets plugin for updating passwords";
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;

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
                _logger.Information($"Plugin {Name} has been successfully configured.");
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
                _logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            if (_configuration != null && credential != null)
            {
                try
                {
                    _secretsClient = new VaultConnection(_address, credential, _logger);
                    _logger.Information($"Plugin {Name} successfully authenticated.");
                }
                catch (Exception ex)
                {
                    _logger.Information(ex, $"Invalid configuration for {Name}. Please use the api to set a valid configuration. {ex.Message}");
                }

                if (_configuration[OrganizationIdName] != null)
                {
                    try
                    {
                        var response = _secretsClient.InvokeMethodFull(Method.GET, $"/context?owner-id={_configuration[OrganizationIdName]}");
                        var contexts = JsonHelper.DeserializeObject<ContextItems>(response.Body);
                        if (contexts?.items != null)
                        {
                            _contextItem = contexts.items.ToArray()
                                .FirstOrDefault(x => x.name.Equals(_configuration[ContextName]), null);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
                    }
                }

                if (_vcsType != null && _vcsOrganization != null && _vcsProject != null)
                {
                    try
                    {
                        var response = _secretsClient.InvokeMethodFull(Method.GET, $"/project/{_vcsType}/{_vcsOrganization}/{_vcsProject}");
                        var project = JsonHelper.DeserializeObject<ProjectItem>(response.Body);
                        _vcsSlug = project?.slug;
                    }
                    catch (Exception ex)
                    {
                        _vcsSlug = null;
                        _logger.Error(ex, $"Failed the connection test for {DisplayName}: {ex.Message}.");
                    }
                }
            }
            else
            {
                _logger.Error("The plugin configuration or credential is missing.");
            }
        }

        public bool TestVaultConnection()
        {
            if (_secretsClient == null)
                return false;

            if (_contextItem == null && _vcsSlug == null)
            {
                _logger.Information($"Organization context {_configuration[ContextName] ?? "undefined"} not found for {DisplayName} or project not found for {_configuration[RepositoryUrlName] ?? "undefined"}.");
                return false;
            }

            // If we got a context or a slug, then we were able to connect using the credentials during the
            //  SetVaultCredential() callback above.
            return true;
        }

        public bool SetPassword(string asset, string account, string password, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.Password)
            {
                _logger.Error("This plugin instance does not handle the Password credential type.");
                return false;
            }

            if (_configuration == null || _secretsClient == null || (_contextItem == null && _vcsSlug == null))
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}_{account}", "_");

            return StoreCredential(name, "{\"value\":\""+password+"\"}", "{\"name\":\""+name+"\",\"value\":\""+password+"\"}");
        }

        public bool SetSshKey(string asset, string account, string sshKey, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.SshKey)
            {
                _logger.Error("This plugin instance does not handle the SshKey credential type.");
                return false;
            }

            if (_configuration == null || _secretsClient == null || (_contextItem == null && _vcsSlug == null))
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
            }

            var name = _rgx.Replace(altAccountName ?? $"{asset}_{account}", "_") + "_sshkey";

            return StoreCredential(name, "{\"value\":\""+sshKey+"\"}", "{\"name\":\""+name+"\",\"value\":\""+sshKey+"\"}");
        }

        public bool SetApiKey(string asset, string account, string[] apiKeys, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.ApiKey)
            {
                _logger.Error("This plugin instance does not handle the ApiKey credential type.");
                return false;
            }

            if (_configuration == null || _secretsClient == null || (_contextItem == null && _vcsSlug == null))
            {
                _logger.Error("No vault connection. Make sure that the plugin has been configured.");
                return false;
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
                    _logger.Error($"The ApiKey {name} {apiKey.ClientId} failed to save to the {this.DisplayName} vault.");
                    retval = false;
                }
            }

            return retval;
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Unload()
        {
            _logger = null;
            _secretsClient = null;
            _configuration.Clear();
            _configuration = null;
        }

        private bool StoreCredential(string name, string contextPayload, string projectPayload)
        {
            var retval = true;

            if (_contextItem != null)
            {
                try
                {
                    var response = _secretsClient.InvokeMethodFull(Method.PUT, $"/context/{_contextItem.id}/environment-variable/{name}", contextPayload);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        _logger.Information($"The secret for {name} has been successfully stored in the context.");
                    }
                    else
                    {
                        _logger.Error($"Failed to set the context secret for {name}: {response.Body}.");
                        retval = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to set the context secret for {name}: {ex.Message}.");
                    retval = false;
                }
            }

            if (_vcsSlug != null)
            {
                try
                {
                    var response = _secretsClient.InvokeMethodFull(Method.POST, $"/project/{_vcsSlug}/envvar", projectPayload);

                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                    {
                        _logger.Information($"The secret for {name} has been successfully stored in the project environment.");
                    }
                    else
                    {
                        _logger.Error($"Failed to set the project environment secret for {name}: {response.Body}.");
                        retval = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to set the project environment secret for {name}: {ex.Message}.");
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
