namespace OneIdentity.DevOps.Data
{
    public class SafeguardConnection
    {
        public string ApplianceAddress { get; set; }
        public bool IgnoreSsl { get; set; }
        public int ApiVersion { get; set; }
        public string ApplianceId { get; set; }
        public string ApplianceName { get; set; }
        public string ApplianceVersion { get; set; }
        public string ApplianceState { get; set; }
    }
}
