using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Xml.Linq;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.SmsTextEmail
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private Dictionary<string,string> _configuration;

        private const string SmtpServer = "smtp.server.test";
        private const string FromAddress = "someone@somewhere.test";
        private const string ToAddresses = "cell-number@cell-carrier.test; cell-number2@cell-carrier.test";

        private const string SmtpServerAddressName = "smtp-server";
        private const string FromAddressName = "from-address";
        private const string ToAddressesName = "to-addresses";

        private string[] _toAddresses;

        public string Name => "SmsTextEmail";
        public string DisplayName => "Sms Text & Email";
        public string Description => "This is the SMS email to text plugin. DO NOT USE IN PRODUCTION";
        public bool SupportsReverseFlow => false;
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};

        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;
        public bool ReverseFlowEnabled { get; set; } = false;
        public ILogger Logger { get; set; }

        public Dictionary<string,string> GetPluginInitialConfiguration()
        {
            return _configuration ??= new Dictionary<string, string>
            {
                { SmtpServerAddressName, SmtpServer },
                { FromAddressName, FromAddress },
                { ToAddressesName, ToAddresses }
            };
        }

        public void SetPluginConfiguration(Dictionary<string,string> configuration)
        {
            if (configuration != null && configuration.ContainsKey(SmtpServerAddressName) && 
                configuration.ContainsKey(FromAddressName) &&
                configuration.ContainsKey(ToAddressesName))
            {
                _configuration = configuration;
                _toAddresses = configuration[ToAddressesName].Split(';');

                if (_toAddresses.Length == 0)
                    Logger.Information("No recipient addresses were found.");

                Logger.Information($"Plugin {Name} has been successfully configured.");
            }
            else
            {
                Logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            Logger.Information($"Plugin {Name} successfully authenticated.");
        }

        public bool TestVaultConnection()
        {
            Logger.Information($"Successfully passed the connection test for Password for {DisplayName}.");
            return true;
        }

        public string GetCredential(CredentialType credentialType, string asset, string account, string altAccountName)
        {
            switch (credentialType)
            {
                case CredentialType.Password:
                    return GetPassword(asset, account, altAccountName);
                case CredentialType.SshKey:
                    return GetSshKey(asset, account, altAccountName);
                case CredentialType.ApiKey:
                    Logger.Error($"The {DisplayName} plugin instance does not fetch the ApiKey credential type.");
                    break;
                default:
                    Logger.Error($"Invalid credential type requested from the {DisplayName} plugin instance.");
                    break;
            }

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
            _configuration.Clear();
            _configuration = null;
        }

        private string GetPassword(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            return null;
        }

        private string GetSshKey(string asset, string account, string altAccountName)
        {
            if (!ValidationHelper.CanReverseFlow(this) || !ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            return null;
        }

        private string SetPassword(string asset, string account, string[] password, string altAccountName)
        {
            if (!ValidationHelper.CanHandlePassword(this))
            {
                return null;
            }

            if (password is not { Length: 1 })
            {
                Logger.Error($"Invalid or null credential sent to {DisplayName} plugin.");
                return null;
            }

            var message = new MailMessage()
            {
                From = new MailAddress(_configuration[FromAddressName]),
                Subject = "Message from Safeguard Secrets Broker for DevOps",
                Body = altAccountName != null ? $"{altAccountName}\n{password[0]}" : $"{asset} - {account}\n{password[0]}"
            };

            StoreCredential(message);
            return password[0];
        }

        private string SetSshKey(string asset, string account, string[] sshKey, string altAccountName)
        {
            if (!ValidationHelper.CanHandleSshKey(this))
            {
                return null;
            }

            var message = new MailMessage()
            {
                From = new MailAddress(_configuration[FromAddressName]),
                Subject = "Message from Safeguard Secrets Broker for DevOps",
                Body = altAccountName != null ? $"{altAccountName}\n{sshKey[0]}" : $"{asset} - {account}\n{sshKey[0]}"
            };

            StoreCredential(message);
            return sshKey[0];
        }

        private string SetApiKey(string asset, string account, string[] apiKeys, string altAccountName)
        {
            if (!ValidationHelper.CanHandleApiKey(this))
            {
                return null;
            }

            var body = string.Empty;
            foreach (var apiKeyJson in apiKeys)
            {
                var apiKey = JsonHelper.DeserializeObject<ApiKey>(apiKeyJson);
                if (apiKey != null)
                {
                    body += altAccountName != null
                        ? $"{altAccountName}\n{apiKey.ClientId}\n{apiKey.ClientSecret}"
                        : $"{asset} - {account} - {apiKey.Name}\n{apiKey.ClientId}\n{apiKey.ClientSecret}\n";
                }
                else
                {
                    Logger.Error($"The ApiKey {asset} - {account} - {apiKey.Name} {apiKey.ClientId} failed to save to the {DisplayName} vault.");
                }
            }

            var message = new MailMessage()
            {
                From = new MailAddress(_configuration[FromAddressName]),
                Subject = "Message from Safeguard Secrets Broker for DevOps",
                Body = body
            };

            return StoreCredential(message);
        }

        private string StoreCredential(MailMessage message)
        {
            foreach (var address in _toAddresses)
            {
                message.To.Add(new MailAddress(address));
            }
            
            var client = new SmtpClient(_configuration[SmtpServerAddressName]);
            client.Send(message);

            return null;
        }
    }
}
