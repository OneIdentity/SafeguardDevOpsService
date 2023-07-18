using System.Collections.Generic;
using OneIdentity.DevOps.Data;
#pragma warning disable 1591

namespace OneIdentity.DevOps.Logic
{
    public interface IMonitoringLogic
    {
        void EnableMonitoring(bool enable);
        void EnableMonitoring(FullMonitorState FullmonitorState);
        MonitorState GetMonitorState();
        FullMonitorState GetFullMonitorState();
        IEnumerable<MonitorEvent> GetMonitorEvents(int size);
        bool PollReverseFlow();
        void Run();
        ReverseFlowMonitorState GetReverseFlowMonitorState();
        ReverseFlowMonitorState SetReverseFlowMonitorState(ReverseFlowMonitorState reverseFlowMonitorState);
        bool ReverseFlowMonitoringAvailable();
    }
}
