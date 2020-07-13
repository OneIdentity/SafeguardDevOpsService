namespace OneIdentity.DevOps.Logic
{
    internal sealed class RestartManager
    {
        private static readonly RestartManager instance = new RestartManager();
        
        private RestartManager()
        {
        }

        public static RestartManager Instance => instance;

        public bool ShouldRestart { get; set; } = false;
    }
}
