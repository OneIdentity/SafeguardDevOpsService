using System.Security;

namespace OneIdentity.SafeguardDevOpsService.Data
{
    internal class EventInfo
    {
        public SecureString ApiKey { get; set; }
        public string AssetName { get; set; }
        public string AccountName { get; set; }
        public override string ToString() => $"{AssetName}/{AccountName}";
    }
}
