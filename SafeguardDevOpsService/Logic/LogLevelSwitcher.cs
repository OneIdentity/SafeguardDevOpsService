
using Serilog.Core;

namespace OneIdentity.DevOps.Logic
{
    internal class LogLevelSwitcher
    {
        private static LogLevelSwitcher _instance;
        private static readonly object InstanceLock = new object();

        public LoggingLevelSwitch LogLevelSwitch { get; } = new LoggingLevelSwitch();

        public static LogLevelSwitcher Instance
        {
            get
            {
                lock (InstanceLock)
                {
                    return _instance ??= new LogLevelSwitcher();
                }
            }
        }
    }
}
