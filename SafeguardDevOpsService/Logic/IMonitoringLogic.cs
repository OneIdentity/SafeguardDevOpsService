using OneIdentity.DevOps.Data;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IMonitoringLogic
    {
        void EnableMonitoring(bool enable);
        MonitorState GetMonitorState();
        void Run();
    }
}
