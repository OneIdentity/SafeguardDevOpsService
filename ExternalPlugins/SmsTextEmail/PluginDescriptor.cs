using System.Collections.Generic;
using System.Net.Mail;
using OneIdentity.DevOps.Common;
using Serilog;

namespace OneIdentity.DevOps.SmsTextEmail
{
    public class PluginDescriptor : ILoadablePlugin
    {
        private static Dictionary<string,string> _configuration;
        private static ILogger _logger;

        private const string SmtpServer = "smtp.server.test";
        private const string FromAddress = "someone@somewhere.test";
        private const string ToAddresses = "cell-number@cell-carrier.test; cell-number2@cell-carrier.test";

        private const string SmtpServerAddressName = "smtp-server";
        private const string FromAddressName = "from-address";
        private const string ToAddressesName = "to-addresses";

        private string[] toAddresses = null;

        public string Name => "SmsTextEmail";
        public string DisplayName => "Sms Text & Email";
        public string Description => "This is the SMS email to text plugin";

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
                toAddresses = configuration[ToAddressesName].Split(';');

                if (toAddresses.Length == 0)
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

        public bool SetPassword(string asset, string account, string password)
        {
            var message = new MailMessage()
            {
                From = new MailAddress(_configuration[FromAddressName]),
                Subject = "Message from Safeguard Secrets Broker for DevOps",
                Body = $"{asset} - {account}\n{password}"
            };

            foreach (var address in toAddresses)
            {
                message.To.Add(new MailAddress(address));
            }
            
            var client = new SmtpClient(_configuration[SmtpServerAddressName]);
            client.Send(message);

            return true;
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
    }
}
