#pragma warning disable 1591

namespace OneIdentity.DevOps.Data.Spp
{
    public class Identity
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public int IdentityProviderId { get; set; }
        public string IdentityProviderName { get; set; }
        public string Name { get; set; }
        public string DomainName { get; set; }
    }
}
