
namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Monitor state
    /// </summary>
    public class MonitorState
    {
        /// <summary>
        /// Is monitor enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Is reverse flow monitor enabled
        /// </summary>
        public bool ReverseFlowEnabled { get; set; }
    }
}
