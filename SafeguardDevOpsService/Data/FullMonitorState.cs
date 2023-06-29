
namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Full monitor state
    /// </summary>
    public class FullMonitorState : MonitorState
    {
        /// <summary>
        /// Reverse flow monitor state
        /// </summary>
        public ReverseFlowMonitorState ReverseFlowMonitorState { get; set; }
    }
}
