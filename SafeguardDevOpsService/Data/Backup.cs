namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Represents a Secrets Broker backup 
    /// </summary>
    public class Backup
    {
        /// <summary>
        /// Base64 representation of the backup (write-only)
        /// </summary>
        public string Base64BackupData { get; set; }
    }
}
