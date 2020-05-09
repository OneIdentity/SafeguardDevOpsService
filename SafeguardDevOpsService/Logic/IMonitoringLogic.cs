using OneIdentity.DevOps.Data;

namespace OneIdentity.DevOps.Logic
{
    public interface IMonitoringLogic
    {
        void EnableMonitoring(bool enable);
        MonitorState GetMonitorState();
    }
}
