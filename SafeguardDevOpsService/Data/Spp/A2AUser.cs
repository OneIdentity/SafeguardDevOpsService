
using OneIdentity.DevOps.Logic;

namespace OneIdentity.DevOps.Data.Spp
{
    public class A2AUser
    {
        public int Id { get; set; }
        public string UserName { get; set; } = WellKnownData.DevOpsUserName;
        public string Description { get; set; } = "Safeguard User for DevOps Service";
        public string DisplayName { get; set; } = "Safeguard DevOps User";
        public string LastName { get; set; } = "DevOps Service";
        public string FirstName { get; set; } = "DevOps User";
        public bool Disabled { get; set; } = false;
        public bool Locked { get; set; } = false;
        public bool PasswordNeverExpires { get; set; } = true;
        public int PrimaryAuthenticationProviderId { get; set; } = -2;
        public string PrimaryAuthenticationIdentity { get; set; }  //Thumbprint
        public string TimeZoneId { get; set; } = "UTC";
        public bool RequireCertificateAuthentication { get; set; } = true;
        public int IdentityProviderId { get; set; } = -1;
        public string IdentityProviderName { get; set; }
        public bool AllowPersonalAccounts { get; set; } = false;
        public string[] AdminRoles { get; set; }
    }
}
