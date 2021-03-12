using System.Collections.Generic;
using OneIdentity.DevOps.Data;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IMonitoringLogic
    {
        void EnableMonitoring(bool enable);
        MonitorState GetMonitorState();
        IEnumerable<MonitorEvent> GetMonitorEvents(int size);
        void Run();
    }
}
