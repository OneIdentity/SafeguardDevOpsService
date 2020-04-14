namespace OneIdentity.DevOps.Data.Spp
{
    internal class LoggedInUser
    {
        public int Id { get; set; }
        public string IdentityProviderName { get; set; }
        public string UserName { get; set; }
        public string EmailAddress { get; set; }
        public string[] AdminRoles { get; set; }
    }
}
