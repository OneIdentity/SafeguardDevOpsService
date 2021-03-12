using System;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Monitor event.
    /// </summary>
    public class MonitorEvent
    {
        /// <summary>
        /// Event description.
        /// </summary>
        public string Event { get; set; }

        /// <summary>
        /// Event result.
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// Event date.
        /// </summary>
        public DateTime Date { get; set; }

    }
}
