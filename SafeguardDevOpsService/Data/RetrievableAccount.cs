
namespace OneIdentity.SafeguardDevOpsService.Data
{
    public class RetrievableAccount
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; }
        public string ApiKey { get; set; }
        public int SystemId { get; set; }
        public string SystemName { get; set; }
        public string CertificateUserThumbPrint { get; set; }
        public string NetworkAddress { get; set; }
        public string DomainName { get; set; }
    }
}
