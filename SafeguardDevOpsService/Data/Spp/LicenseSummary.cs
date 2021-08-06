using System;

namespace OneIdentity.DevOps.Data.Spp
{
    /// <summary>
    /// Represents a license summary
    /// </summary>
    public class LicenseSummary
    {
        /// <summary>
        /// The license type.
        /// </summary>
        public LicenseType Type { get; set; }

        /// <summary>
        /// The module that this license applies to.
        /// </summary>
        public LicensableModule Module { get; set; }

        /// <summary>
        /// Is the license currently valid. This will be false if the license
        /// has expired.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// The date that this license expires, or null
        /// if it is perpetual.
        /// </summary>
        public DateTimeOffset? Expires { get; set; }
    }
}
