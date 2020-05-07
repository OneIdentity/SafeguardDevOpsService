namespace OneIdentity.DevOps.Data.Spp
{
    public class SppAccountWrapper
    {
        public SppAccount Account { get; set; }
    }

    public class SppAccount
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DomainName { get; set; }
        public bool HasPassword { get; set; }
        public bool Disabled { get; set; }
        public int SystemId { get; set; }
        public string SystemName { get; set; }
        public string SystemNetworkAddress { get; set; }
        public bool AllowPasswordRequest { get; set; }
        public bool AllowSessionRequest { get; set; }
    }
}
