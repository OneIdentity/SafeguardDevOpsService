namespace OneIdentity.DevOps.Data
{
    public class SafeguardConnectionRequest
    {
        public string NetworkAddress { get; set; }
        public string AccessToken { get; set; }
        public bool IgnoreSsl { get; set; }
    }
}
