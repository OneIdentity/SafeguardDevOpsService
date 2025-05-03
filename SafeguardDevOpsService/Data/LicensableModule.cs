namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Type of License Module
    /// </summary>
    public enum LicensableModule
    {
        /// <summary>
        /// Password Management
        /// </summary>
        PasswordManagement = 1,
        /// <summary>
        /// Session Management
        /// </summary>
        SessionManagement = 2,
        /// <summary>
        /// Secrets Broker
        /// </summary>
        SecretsBroker = 3,

        /// <summary>The paid for version of Personal Password Vault.</summary>
        EnterpriseAccount = 4,

        /// <summary>Starling Connect for Assets.</summary>
        DisconnectedAssets = 5,
    }
}
