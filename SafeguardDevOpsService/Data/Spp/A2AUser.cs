#pragma warning disable 1591

namespace OneIdentity.DevOps.Data.Spp
{
    public class A2AUser
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = "Safeguard User for Safeguard Secrets Broker for DevOps";
        public string DisplayName { get; set; } = "Safeguard Secrets Broker for DevOps User";
        public string LastName { get; set; } = "Secrets Broker for DevOps";
        public string FirstName { get; set; } = "Safeguard";
        public bool Disabled { get; set; } = false;
        public bool Locked { get; set; } = false;
        public bool PasswordNeverExpires { get; set; } = true;
        public AuthenticationProvider PrimaryAuthenticationProvider { get; set; }
        public string TimeZoneId { get; set; } = "UTC";
        public bool RequireCertificateAuthentication { get; set; } = true;
        public AuthenticationProvider IdentityProvider { get; set; }
        public bool AllowPersonalAccounts { get; set; } = false;
        public string[] AdminRoles { get; set; }
    }

    public class AuthenticationProvider
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Identity { get; set; }
    }
}
