
namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Reverse flow monitor state
    /// </summary>
    public class ReverseFlowMonitorState
    {
        /// <summary>
        /// Is reverse flow monitor enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Reverse flow polling interval
        /// </summary>
        public int ReverseFlowPollingInterval { get; set; }
    }
}
