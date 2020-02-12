using System;

namespace OneIdentity.DevOps.Data
{
    /// <summary>
    /// Implement this interface to add an internal mini-service to
    /// a Safeguard service application.
    /// </summary>
    public interface IService : IDisposable
    {
        /// <summary>
        /// Runs the service.
        /// </summary>
        /// <param name="token">Cancellation token indicating that the service should stop.</param>
        /// <returns></returns>
        void Run();

//        void Stop();
//
//        /// <summary>
//        /// Implementations should provide notification when startup is complete.
//        /// </summary>
//        IObservable<bool> StartupComplete { get;  }
//
//        /// <summary>
//        /// True if startup is complete
//        /// </summary>
//        bool IsStartupComplete { get; }
//
//        /// <summary>
//        /// If true, the service is long running and the task returned by Run should not complete
//        /// until the cancellation token is set. If it does, it will cause the hosting application
//        /// to crash. This is intentional as we don't want mini-services to stop unexpectedly.
//        ///
//        /// If your mini-service can reasonably complete early, return false.
//        /// </summary>
//        bool IsLongRunning { get; }

        /// <summary>
        /// Service display name
        /// </summary>
        string ServiceName { get; }
    }
}
