using System;
using System.Collections.Generic;
using System.Net.Mail;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.SmsTextEmail
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private Dictionary<string,string> _configuration;
        private ILogger _logger;

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
        public CredentialType[] SupportedCredentialTypes => new[] {CredentialType.Password, CredentialType.SshKey, CredentialType.ApiKey};
        public CredentialType AssignedCredentialType { get; set; } = CredentialType.Password;

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
                    _logger.Information("No recipient addresses were found.");

                _logger.Information($"Plugin {Name} has been successfully configured.");
            }
            else
            {
                _logger.Error("Some parameters are missing from the configuration.");
            }
        }

        public void SetVaultCredential(string credential)
        {
            _logger.Information($"Plugin {Name} successfully authenticated.");
        }

        public bool SetPassword(string asset, string account, string password, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.Password)
            {
                _logger.Error("This plugin instance does not handle the Password credential type.");
                return false;
            }

            var message = new MailMessage()
            {
                From = new MailAddress(_configuration[FromAddressName]),
                Subject = "Message from Safeguard Secrets Broker for DevOps",
                Body = altAccountName != null ? $"{altAccountName}\n{password}" : $"{asset} - {account}\n{password}"
            };

            return StoreCredential(message);
        }

        public bool SetSshKey(string asset, string account, string sshKey, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.SshKey)
            {
                _logger.Error("This plugin instance does not handle the SshKey credential type.");
                return false;
            }

            var message = new MailMessage()
            {
                From = new MailAddress(_configuration[FromAddressName]),
                Subject = "Message from Safeguard Secrets Broker for DevOps",
                Body = altAccountName != null ? $"{altAccountName}\n{sshKey}" : $"{asset} - {account}\n{sshKey}"
            };

            return StoreCredential(message);
        }

        public bool SetApiKey(string asset, string account, string[] apiKeys, string altAccountName = null)
        {
            if (AssignedCredentialType != CredentialType.ApiKey)
            {
                _logger.Error("This plugin instance does not handle the ApiKey credential type.");
                return false;
            }

            // Need to send all of the keys. For this plugin just concatenate them all together and send one text.
            var message = new MailMessage()
            {
                From = new MailAddress(_configuration[FromAddressName]),
                Subject = "Message from Safeguard Secrets Broker for DevOps",
                //Body = altAccountName != null ? $"{altAccountName}\n{clientId}\n{clientSecret}" : $"{asset} - {account}\n{clientId}\n{clientSecret}"
                Body = altAccountName != null ? $"{altAccountName}\n{string.Empty}\n{string.Empty}" : $"{asset} - {account}\n{string.Empty}\n{string.Empty}"
            };

            return StoreCredential(message);
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public bool TestVaultConnection()
        {
            _logger.Information($"Successfully passed the connection test for Password for {DisplayName}.");
            return true;
        }

        public void Unload()
        {
            _logger = null;
            _configuration.Clear();
            _configuration = null;
        }

        private bool StoreCredential(MailMessage message)
        {
            foreach (var address in _toAddresses)
            {
                message.To.Add(new MailAddress(address));
            }
            
            var client = new SmtpClient(_configuration[SmtpServerAddressName]);
            client.Send(message);

            return true;
        }
    }
}
