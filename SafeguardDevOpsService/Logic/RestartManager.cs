namespace OneIdentity.DevOps.Logic
{
    internal sealed class RestartManager
    {
        private static readonly RestartManager Inst = new RestartManager();
        
        private RestartManager()
        {
        }

        public static RestartManager Instance => Inst;

        public bool ShouldRestart { get; set; } = false;
    }
}
